using SSX_Library.Internal.Utilities;

namespace SSX_Library.Internal;

/*
    More Info on the bitstream structure: http://wiki.niotso.org/RefPack
*/

internal static class Refpack
{
    /// <summary>
    /// Peeks at the current stream position to check for a Refpack signature, 
    /// restoring the stream position before returning.
    /// </summary>
    public static bool HasRefpackSignature(Stream stream)
    {
        byte[] buf = Reader.ReadBytes(stream, 2);
        stream.Position -= buf.Length;
        return buf.Length == 2 && buf[1] == 0xFB;
    }

    /// <summary>
    /// Decompresses a Refpack compressed array of bytes.
    /// </summary>
    public static byte[] Decompress(byte[] inputData)
    {
        if (inputData.Length == 0)
        {
            throw new ArgumentException("Input data cannot be empty.");
        }

        using MemoryStream inputStream = new(inputData);

        // Header
        byte magicFlags = Reader.ReadByte(inputStream);
        byte signature = Reader.ReadByte(inputStream);

        if (signature != 0xFB)
        {
            throw new InvalidDataException("Invalid Refpack signature. Expected 0xFB.");
        }

        bool isLongSize = (magicFlags & 0x80) != 0;
        bool hasCompressedSize = (magicFlags & 0x01) != 0;

        uint decompressedSize = isLongSize 
            ? Reader.ReadUInt32(inputStream, ByteOrder.BigEndian) 
            : Reader.ReadUInt24(inputStream, ByteOrder.BigEndian);

        if (hasCompressedSize)
        {
            // Skip compressed size if present. We dont need it for decompression.
            _ = isLongSize 
                ? Reader.ReadUInt32(inputStream, ByteOrder.BigEndian) 
                : Reader.ReadUInt24(inputStream, ByteOrder.BigEndian);
        }

        byte[] outputData = new byte[decompressedSize];
        int outputPos = 0;

        // Safety check to prevent infinite loop on corrupted data.
        while (outputPos < decompressedSize) 
        {
            byte first = Reader.ReadByte(inputStream);

            if ((first & 0x80) == 0) // Command 2 (Unary 0)
            {
                byte second = Reader.ReadByte(inputStream);

                DecompressCommand command = new()
                {
                    ProceedingDataLength = first & 0x03,
                    ReferencedDataDistance = ((first & 0x60) << 3) + second + 1,
                    ReferenceDataLength = ((first & 0x1C) >> 2) + 3
                };

                CopyLiteralAndMatch(inputStream, outputData, ref outputPos, command);
            }
            else if ((first & 0x40) == 0) // Command 3 (Unary 10)
            {
                byte second = Reader.ReadByte(inputStream);
                byte third = Reader.ReadByte(inputStream);

                DecompressCommand command = new()
                {
                    ProceedingDataLength = second >> 6,
                    ReferencedDataDistance = ((second & 0x3F) << 8) + third + 1,
                    ReferenceDataLength = (first & 0x3F) + 4
                };

                CopyLiteralAndMatch(inputStream, outputData, ref outputPos, command);
            }
            else if ((first & 0x20) == 0) // Command 4 (Unary 110)
            {
                byte second = Reader.ReadByte(inputStream);
                byte third = Reader.ReadByte(inputStream);
                byte fourth = Reader.ReadByte(inputStream);

                DecompressCommand command = new()
                {
                    ProceedingDataLength = first & 0x03,
                    ReferencedDataDistance = ((first & 0x10) << 12) + (second << 8) + third + 1,
                    ReferenceDataLength = ((first & 0x0C) << 6) + fourth + 5
                };

                CopyLiteralAndMatch(inputStream, outputData, ref outputPos, command);
            }
            else // Unary 111
            {
                if (first < 0xFC) // Command 1
                {
                    int proceedingDataLength = (first & 0x1F) * 4 + 4;
                    for (int _ = 0; _ < proceedingDataLength; _++)
                    {
                        outputData[outputPos++] = Reader.ReadByte(inputStream);
                    }
                }
                else // Stop Command
                {
                    int proceedingDataLength = first & 0x03;
                    for (int _ = 0; _ < proceedingDataLength; _++)
                    {
                        outputData[outputPos++] = Reader.ReadByte(inputStream);
                    }
                    break;
                }
            }
        }

        return outputData;
    }

    /// <summary>
    /// Compresses an array of bytes to Refpack.
    /// </summary>
    public static byte[] Compress(byte[] inputData)
    {
        bool endIsValid = false;
        List<byte[]> compressedChunks = [];
        int compressedIndex = 0;
        int compressedLength = 0;

        // Is data too small to compress
        if (inputData.Length < 16)
        {
            return inputData;
        }

        Queue<KeyValuePair<int, int>> blockTrackingQueue = new();
        Queue<KeyValuePair<int, int>> blockPretrackingQueue = new();

        // Used to prevent constant allocation/freeing
        Queue<List<int>> unusedLists = new();
        Dictionary<int, List<int>> latestBlocks = [];
        int lastBlockStored = 0;

        byte[] output;
        while (compressedIndex < inputData.Length)
        {
            while (compressedIndex > lastBlockStored + CompressionLevelMax.BlockInterval
            && inputData.Length - compressedIndex > 16)
            {
                if (blockPretrackingQueue.Count >= CompressionLevelMax.PrequeueLength)
                {
                    var tmpPair = blockPretrackingQueue.Dequeue();
                    blockTrackingQueue.Enqueue(tmpPair);

                    if (!latestBlocks.TryGetValue(tmpPair.Key, out List<int>? valueList) || valueList == null)
                    {
                        valueList = unusedLists.Count > 0 ? unusedLists.Dequeue() : [];
                        latestBlocks[tmpPair.Key] = valueList;
                    }

                    if (valueList.Count >= CompressionLevelMax.SameValToTrack)
                    {
                        int earliestIndex = 0;
                        int earliestValue = valueList[0];
                        for (int i = 1; i < valueList.Count; i++)
                        {
                            if (valueList[i] < earliestValue)
                            {
                                earliestIndex = i;
                                earliestValue = valueList[i];
                            }
                        }
                        valueList[earliestIndex] = tmpPair.Value;
                    }
                    else
                    {
                        valueList.Add(tmpPair.Value);
                    }

                    if (blockTrackingQueue.Count > CompressionLevelMax.QueueLength)
                    {
                        var tmpPair2 = blockTrackingQueue.Dequeue();
                        valueList = latestBlocks[tmpPair2.Key];

                        for (int i = 0; i < valueList.Count; i++)
                        {
                            if (valueList[i] == tmpPair2.Value)
                            {
                                valueList.RemoveAt(i);
                                break;
                            }
                        }
                        if (valueList.Count == 0)
                        {
                            latestBlocks.Remove(tmpPair2.Key);
                            unusedLists.Enqueue(valueList);
                        }
                    }
                }

                KeyValuePair<int, int> newBlock = new(
                    BitConverter.ToInt32(inputData,
                    lastBlockStored),
                    lastBlockStored
                );
                lastBlockStored += CompressionLevelMax.BlockInterval;
                blockPretrackingQueue.Enqueue(newBlock);
            }

            if (inputData.Length - compressedIndex < 4)
            {
                // Just copy the rest
                byte[] chunk = new byte[inputData.Length - compressedIndex + 1];
                chunk[0] = (byte)(0xFC | (inputData.Length - compressedIndex));
                Array.Copy(inputData, compressedIndex, chunk, 1, inputData.Length - compressedIndex);
                compressedChunks.Add(chunk);
                compressedIndex += chunk.Length - 1;
                compressedLength += chunk.Length;
                endIsValid = true;
                continue;
            }

            // Search ahead the next 3 bytes for the "best" sequence to copy
            var sequenceStart = 0;
            var sequenceLength = 0;
            var sequenceIndex = 0;
            var isSequence = false;
            if (FindSequence(
                inputData,
                compressedIndex,
                ref sequenceStart,
                ref sequenceLength,
                ref sequenceIndex,
                latestBlocks))
            {
                isSequence = true;
            }
            else
            {
                // Find the next sequence
                for (
                    int i = compressedIndex + 4;
                    !isSequence && i + 3 < inputData.Length;
                    i += 4)
                {
                    if (FindSequence(
                        inputData,
                        i,
                        ref sequenceStart,
                        ref sequenceLength,
                        ref sequenceIndex,
                        latestBlocks))
                    {
                        sequenceIndex += i - compressedIndex;
                        isSequence = true;
                    }
                }
                if (sequenceIndex == int.MaxValue)
                {
                    sequenceIndex = inputData.Length - compressedIndex;
                }

                // Copy all the data skipped over
                while (sequenceIndex >= 4)
                {
                    int toCopy = sequenceIndex & ~3;
                    if (toCopy > 112)
                    {
                        toCopy = 112;
                    }

                    byte[] chunk = new byte[toCopy + 1];
                    chunk[0] = (byte)(0xE0 | ((toCopy >> 2) - 1));
                    Array.Copy(inputData, compressedIndex, chunk, 1, toCopy);
                    compressedChunks.Add(chunk);
                    compressedIndex += toCopy;
                    compressedLength += chunk.Length;
                    sequenceIndex -= toCopy;
                }
            }

            /*
            * 00-7F  0oocccpp oooooooo
            *   Read 0-3
            *   Copy 3-10
            *   Offset 0-1023
            *   
            * 80-BF  10cccccc ppoooooo oooooooo
            *   Read 0-3
            *   Copy 4-67
            *   Offset 0-16383
            *   
            * C0-DF  110cccpp oooooooo oooooooo cccccccc
            *   Read 0-3
            *   Copy 5-1028
            *   Offset 0-131071
            *   
            * E0-FC  111ppppp
            *   Read 4-128 (Multiples of 4)
            *   
            * FD-FF  111111pp
            *   Read 0-3
            */
            if (isSequence)
            {
                continue;
            }
            if (FindRunLength(inputData, sequenceStart, compressedIndex + sequenceIndex) < sequenceLength)
            {
                break;
            }
            while (sequenceLength > 0)
            {
                int thisLength = sequenceLength;
                if (thisLength > 1028)
                {
                    thisLength = 1028;
                }

                sequenceLength -= thisLength;
                int offset = compressedIndex - sequenceStart + sequenceIndex - 1;

                byte[] chunk;
                if (thisLength > 67 || offset > 16383)
                {
                    chunk = new byte[sequenceIndex + 4];
                    chunk[0] = (byte)(0xC0 | sequenceIndex | (((thisLength - 5) >> 6) & 0x0C) | ((offset >> 12) & 0x10));
                    chunk[1] = (byte)((offset >> 8) & 0xFF);
                    chunk[2] = (byte)(offset & 0xFF);
                    chunk[3] = (byte)((thisLength - 5) & 0xFF);
                }
                else if (thisLength > 10 || offset > 1023)
                {
                    chunk = new byte[sequenceIndex + 3];
                    chunk[0] = (byte)(0x80 | ((thisLength - 4) & 0x3F));
                    chunk[1] = (byte)(((sequenceIndex << 6) & 0xC0) | ((offset >> 8) & 0x3F));
                    chunk[2] = (byte)(offset & 0xFF);
                }
                else
                {
                    chunk = new byte[sequenceIndex + 2];
                    chunk[0] = (byte)((sequenceIndex & 0x3) | (((thisLength - 3) << 2) & 0x1C) | ((offset >> 3) & 0x60));
                    chunk[1] = (byte)(offset & 0xFF);
                }

                if (sequenceIndex > 0)
                {
                    Array.Copy(inputData, compressedIndex, chunk, chunk.Length - sequenceIndex, sequenceIndex);
                }
                compressedChunks.Add(chunk);
                compressedIndex += thisLength + sequenceIndex;
                compressedLength += chunk.Length;
                sequenceStart += thisLength;
                sequenceIndex = 0;
            }
        }

        int chunkPosition;
        if (inputData.Length > 0xFFFFFF)
        {
            output = new byte[compressedLength + 5 + (endIsValid ? 0 : 1)];
            output[0] = 0x10 | 0x80; // 0x80 = length is 4 bytes
            output[1] = 0xFB;
            output[2] = (byte)(inputData.Length >> 24);
            output[3] = (byte)(inputData.Length >> 16);
            output[4] = (byte)(inputData.Length >> 8);
            output[5] = (byte)inputData.Length;
            chunkPosition = 6;
        }
        else
        {
            output = new byte[compressedLength + 5 + (endIsValid ? 0 : 1)];
            output[0] = 0x10;
            output[1] = 0xFB;
            output[2] = (byte)(inputData.Length >> 16);
            output[3] = (byte)(inputData.Length >> 8);
            output[4] = (byte)inputData.Length;
            chunkPosition = 5;
        }

        foreach (byte[] t in compressedChunks)
        {
            Array.Copy(t, 0, output, chunkPosition, t.Length);
            chunkPosition += t.Length;
        }

        if (!endIsValid)
        {
            output[^1] = 0xFC;
        }

        return output;
    }

    private static bool FindSequence(
        byte[] data,
        int offset,
        ref int bestStart,
        ref int bestLength,
        ref int bestIndex,
        Dictionary<int, List<int>> blockTracking)
    {
        int start;
        int end = -CompressionLevelMax.BruteForceLength;

        if (offset < CompressionLevelMax.BruteForceLength)
        {
            end = -offset;
        }

        if (offset > 4)
        {
            start = -3;
        }
        else
        {
            start = offset - 3;
        }

        bool foundRun = false;
        if (bestLength < 3)
        {
            bestLength = 3;
            bestIndex = int.MaxValue;
        }

        byte[] search = new byte[data.Length - offset > 4 ? 4 : data.Length - offset];
        for (int i = 0; i < search.Length; i++)
        {
            search[i] = data[offset + i];
        }

        while (start >= end && bestLength < 1028)
        {
            byte currentByte = data[start + offset];
            for (int i = 0; i < search.Length; i++)
            {
                if (currentByte != search[i] || start >= i || start - i < -131072)
                {
                    continue;
                }

                int len = FindRunLength(data, offset + start, offset + i);
                if ((len > bestLength || len == bestLength && i < bestIndex) 
                && (len >= 5 || len >= 4 && start - i > -16384 || len >= 3 && start - i > -1024))
                {
                    foundRun = true;
                    bestStart = offset + start;
                    bestLength = len;
                    bestIndex = i;
                }
            }
            start--;
        }

        if (blockTracking.Count > 0 && data.Length - offset > 16 && bestLength < 1028)
        {
            for (int i = 0; i < 4; i++)
            {
                var thisPosition = offset + 3 - i;
                var adjust = i > 3 ? i - 3 : 0;
                var value = BitConverter.ToInt32(data, thisPosition);
                if (blockTracking.TryGetValue(value, out List<int>? positions))
                {
                    foreach (var tryPos in positions)
                    {
                        int localadjust = adjust;
                        if (tryPos + 131072 < offset + 8)
                        {
                            continue;
                        }

                        int length = FindRunLength(data, tryPos + localadjust, thisPosition + localadjust);
                        if (length >= 5 && length > bestLength)
                        {
                            foundRun = true;
                            bestStart = tryPos + localadjust;
                            bestLength = length;
                            if (i < 3)
                            {
                                bestIndex = 3 - i;
                            }
                            else
                            {
                                bestIndex = 0;
                            }
                        }
                        if (bestLength > 1028)
                        {
                            break;
                        }
                    }
                }
                if (bestLength > 1028)
                {
                    break;
                }
            }
        }
        return foundRun;
    }

    private static int FindRunLength(byte[] data, int source, int destination)
    {
        int endSource = source + 1;
        int endDestination = destination + 1;
        while (endDestination < data.Length 
        && data[endSource] == data[endDestination] 
        && endDestination - destination < 1028)
        {
            endSource++;
            endDestination++;
        }
        return endDestination - destination;
    }

    private static void CopyLiteralAndMatch(
        Stream inputStream, 
        byte[] outputData,
        ref int outputPos,
        DecompressCommand command)
    {
        // Copy proceeding literal data.
        for (int _ = 0; _ < command.ProceedingDataLength; _++)
        {
            outputData[outputPos++] = Reader.ReadByte(inputStream);
        }

        // Copy matching reference data.
        if (command.ReferencedDataDistance > outputPos)
        {
            throw new InvalidDataException("Referenced data distance is greater than the current output position. Please report.");
        }

        int matchPos = outputPos - command.ReferencedDataDistance;
        for (int _ = 0; _ < command.ReferenceDataLength; _++)
        {
            outputData[outputPos++] = outputData[matchPos++];
        }
    }

    private struct DecompressCommand
    {
        public int ProceedingDataLength;
        public int ReferencedDataDistance;
        public int ReferenceDataLength;
    }

    public static class CompressionLevelMax
    {
        public const int BlockInterval = 1;
        public const int SearchLength = 1;
        public const int PrequeueLength = SearchLength / BlockInterval;
        public const int QueueLength = 131000 / BlockInterval - PrequeueLength;
        public const int SameValToTrack = 10;
        public const int BruteForceLength = 64;
    }
}
