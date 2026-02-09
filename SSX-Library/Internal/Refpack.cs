using System;
using System.IO;
using System.Collections.Immutable;
using System.Data.SqlTypes;
using SSX_Library.Internal.Utilities;

namespace SSX_Library.Internal;

/*
    More Info on the bitstream structure: http://wiki.niotso.org/RefPack
*/

// TODO: Make internal after finishing.
public static class Refpack
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
        if (inputData.Length == 0)
        {
            throw new ArgumentException("Input data cannot be empty.");
        }

        using MemoryStream outStream = new();

        // Write Header
        bool isLarge = inputData.Length > 0xFFFFFF;
        int magicFlags = isLarge ? 0x90 : 0x10; // 0x80 (isLarge) | 0x10 (default)
        Writer.WriteByte(outStream, (byte)magicFlags);
        Writer.WriteByte(outStream, 0xFB); // Signature

        if (isLarge)
        {
            Writer.WriteUInt32(outStream, (uint)inputData.Length, ByteOrder.BigEndian);
        }
        else
        {
            Writer.WriteUInt24(outStream, (uint)inputData.Length, ByteOrder.BigEndian);
        }

        int pos = 0;
        int length = inputData.Length;
        int proceedingDataLength = 0;
        
        int[] head = new int[65536];
        Array.Fill(head, -1);

        while (pos < length)
        {
            if (FindBestMatch(inputData, pos, length, head, out int matchLen, out int matchDist))
            {
                // Before writing the match, flush literals in chunks of 4 if there are more than 3.
                WriteLiteralRun(outStream, inputData, pos, ref proceedingDataLength);

                // Write the match command (encodes 0-3 remaining literals).
                WriteMatchCommand(outStream, matchLen, matchDist, proceedingDataLength);

                // Write the proceeding literal data bytes.
                if (proceedingDataLength > 0)
                {
                    Writer.WriteBytes(outStream, inputData, pos - proceedingDataLength, proceedingDataLength);
                    proceedingDataLength = 0;
                }

                // Update hash table for the matched range.
                int endPos = pos + matchLen;
                for (int k = pos + 1; k < endPos && k + 3 < length; k++)
                {
                    UpdateHash(inputData, k, head);
                }
                
                pos += matchLen;
            }
            else
            {
                proceedingDataLength++;
                pos++;
                
                // Keep literal runs within reasonable bounds.
                if (proceedingDataLength >= 128) 
                {
                    WriteLiteralRun(outStream, inputData, pos, ref proceedingDataLength);
                }
            }
        }

        // Finalize: encode any leftover literals and the stop command.
        FinishCompression(outStream, inputData, pos, proceedingDataLength);

        return outStream.ToArray();
    }

    private static void UpdateHash(byte[] inputData, int pos, int[] head)
    {
        int h = ((inputData[pos] << 5) ^ (inputData[pos + 1] << 2) ^ inputData[pos + 2]) & 0xFFFF;
        head[h] = pos;
    }

    private static bool FindBestMatch(byte[] inputData, int pos, int length, int[] head, out int matchLen, out int matchDist)
    {
        matchLen = 0;
        matchDist = 0;

        if (pos + 3 >= length) return false;

        int h = ((inputData[pos] << 5) ^ (inputData[pos + 1] << 2) ^ inputData[pos + 2]) & 0xFFFF; // Hash
        int refPos = head[h];
        head[h] = pos; // Update head even if no match for next loop iterations.

        if (refPos == -1) return false;

        int dist = pos - refPos;
        if (dist <= 0 || dist > 131072) return false;

        // Verify the 3-byte prefix.
        if (inputData[refPos] != inputData[pos] || 
            inputData[refPos + 1] != inputData[pos + 1] || 
            inputData[refPos + 2] != inputData[pos + 2])
        {
            return false;
        }

        int len = 3;
        int maxLen = Math.Min(length - pos, 1028);
        while (len < maxLen && inputData[refPos + len] == inputData[pos + len])
        {
            len++;
        }

        // Validate command encoding constraints.
        if (len >= 3 && len <= 10 && dist <= 1024)
        {
            matchLen = len;
            matchDist = dist;
            return true;
        }
        
        if (len >= 4 && len <= 67 && dist <= 16384)
        {
            matchLen = len;
            matchDist = dist;
            return true;
        }

        if (len >= 5) // 4-byte command supports up to 128KB offset.
        {
            matchLen = len;
            matchDist = dist;
            return true;
        }

        return false;
    }

    private static void WriteLiteralRun(Stream outStream, byte[] inputData, int pos, ref int proceedingDataLength)
    {
        while (proceedingDataLength >= 4)
        {
            int run = Math.Min(proceedingDataLength & ~3, 128);
            Writer.WriteByte(outStream, (byte)(0xE0 | ((run - 4) / 4)));
            Writer.WriteBytes(outStream, inputData, pos - proceedingDataLength, run);
            proceedingDataLength -= run;
        }
    }

    private static void WriteMatchCommand(Stream outStream, int matchLen, int matchDist, int proceedingDataLength)
    {
        int d = matchDist - 1;

        if (matchLen <= 10 && matchDist <= 1024)
        {
            // Command 2: 0ccaaabb
            int a = matchLen - 3;
            int b = proceedingDataLength;
            int c = (d >> 8) & 0x03;
            Writer.WriteByte(outStream, (byte)((c << 5) | (a << 2) | b));
            Writer.WriteByte(outStream, (byte)(d & 0xFF));
        }
        else if (matchLen <= 67 && matchDist <= 16384)
        {
            // Command 3: 10aaaaaa bbcccccc dddddd
            int l = matchLen - 4;
            Writer.WriteByte(outStream, (byte)(0x80 | l));
            Writer.WriteByte(outStream, (byte)((proceedingDataLength << 6) | ((d >> 8) & 0x3F)));
            Writer.WriteByte(outStream, (byte)(d & 0xFF));
        }
        else
        {
            // Command 4: 110aaabb cccccccc dddddddd eeeeeeee
            int val = matchLen - 5;
            int fourth = val & 0xFF;
            int highLen = (val >> 8) & 0x03;
            int highOffset = (d >> 16) & 0x01;
            
            byte first = (byte)(0xC0 | (highOffset << 4) | (highLen << 2) | proceedingDataLength);
            Writer.WriteByte(outStream, first);
            Writer.WriteByte(outStream, (byte)((d >> 8) & 0xFF));
            Writer.WriteByte(outStream, (byte)(d & 0xFF));
            Writer.WriteByte(outStream, (byte)fourth);
        }
    }

    private static void FinishCompression(Stream outStream, byte[] inputData, int pos, int proceedingDataLength)
    {
        while (proceedingDataLength > 0)
        {
            if (proceedingDataLength >= 4)
            {
                int run = Math.Min(proceedingDataLength & ~3, 128);
                Writer.WriteByte(outStream, (byte)(0xE0 | ((run - 4) / 4)));
                Writer.WriteBytes(outStream, inputData, pos - proceedingDataLength, run);
                proceedingDataLength -= run;
            }
            else
            {
                // Stop Command encodes 0-3 literals.
                Writer.WriteByte(outStream, (byte)(0xFC | proceedingDataLength));
                Writer.WriteBytes(outStream, inputData, pos - proceedingDataLength, proceedingDataLength);
                proceedingDataLength = 0;
            }
        }

        // Final safety stop command if the stream is empty or ends differently.
        if (outStream.Length > 0)
        {
            Writer.WriteByte(outStream, 0xFC);
        }
    }

    private static void CopyLiteralAndMatch(Stream inputStream, byte[] outputData, ref int outputPos, DecompressCommand command)
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
}
