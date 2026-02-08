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
    private static readonly ImmutableArray<byte> _magic = [0x10, 0xFB];

    /// <summary>
    /// Peeks at the current stream position to check for a Refpack signature, 
    /// restoring the stream position before returning.
    /// </summary>
    public static bool HasRefpackSignature(Stream stream)
    {
        byte[] buf = Reader.ReadBytes(stream, _magic.Length);
        stream.Position -= _magic.Length;
        return buf.SequenceEqual(_magic);
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
                    PreceedingDataLength = first & 0x03,
                    ReferencedDataDistance = ((first & 0x60) << 3) + second + 1,
                    ReferencedDataLength = ((first & 0x1C) >> 2) + 3
                };

                CopyLiteralAndMatch(inputStream, outputData, ref outputPos, command);
            }
            else if ((first & 0x40) == 0) // Command 3 (Unary 10)
            {
                byte second = Reader.ReadByte(inputStream);
                byte third = Reader.ReadByte(inputStream);

                DecompressCommand command = new()
                {
                    PreceedingDataLength = second >> 6,
                    ReferencedDataDistance = ((second & 0x3F) << 8) + third + 1,
                    ReferencedDataLength = (first & 0x3F) + 4
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
                    PreceedingDataLength = first & 0x03,
                    ReferencedDataDistance = ((first & 0x10) << 12) + (second << 8) + third + 1,
                    ReferencedDataLength = ((first & 0x0C) << 6) + fourth + 5
                };

                CopyLiteralAndMatch(inputStream, outputData, ref outputPos, command);
            }
            else // Unary 111
            {
                if (first < 0xFC) // Command 1
                {
                    int preceeding_data_length = (first & 0x1F) * 4 + 4;
                    for (int _ = 0; _ < preceeding_data_length; _++)
                    {
                        outputData[outputPos++] = Reader.ReadByte(inputStream);
                    }
                }
                else // Stop Command
                {
                    int preceeding_data_length = first & 0x03;
                    for (int _ = 0; _ < preceeding_data_length; _++)
                    {
                        outputData[outputPos++] = Reader.ReadByte(inputStream);
                    }
                    break;
                }
            }
        }

        return outputData;
    }

    private static void CopyLiteralAndMatch(Stream inputStream, byte[] outputData, ref int outputPos, DecompressCommand command)
    {
        // Copy literal data (run)
        for (int _ = 0; _ < command.PreceedingDataLength; _++)
        {
            outputData[outputPos++] = Reader.ReadByte(inputStream);
        }

        // Copy referenced data (match)
        if (command.ReferencedDataDistance > outputPos)
        {
            // "If this runs let us know. It is said that they repeat the same
            // reference if the run goes over the end of the output buffer like regular LZSS,
            // But our original source did not include it. Maybe it already does but
            // I cant get my head around it." -Eric
            throw new InvalidDataException("Referenced data distance is greater than the current output position. Please report.");
        }

        int matchPos = outputPos - command.ReferencedDataDistance;
        for (int _ = 0; _ < command.ReferencedDataLength; _++)
        {
            outputData[outputPos++] = outputData[matchPos++];
        }
    }

    private struct DecompressCommand
    {
        public int PreceedingDataLength;
        public int ReferencedDataDistance;
        public int ReferencedDataLength;
    }
}