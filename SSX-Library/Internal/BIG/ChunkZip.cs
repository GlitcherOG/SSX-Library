using SSX_Library.Internal.Extensions;
using SSX_Library.Utilities;
using System.Collections.Immutable;
using System.Text;
using System.IO.Compression;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Helper class for dealing with ChunkZip compression
/// </summary>
internal static class ChunkZip
{
    const int DefaultBlockSize = 128 * 1024; // 128KB in KiB
    private static readonly ImmutableArray<byte> _magic = [.. Encoding.ASCII.GetBytes("chunkzip")];

    /// <summary>
    /// Peeks at the current stream position to check for a ChunkZip signature, 
    /// restoring the stream position before returning.
    /// </summary>
    public static bool HasChunkZipSignature(Stream stream)
    {
        byte[] buf = Reader.ReadBytes(stream, _magic.Length);
        stream.Position -= _magic.Length;
        return buf.SequenceEqual(_magic);
    }

    /// <summary>
    /// Decompress an array of bytes
    /// </summary>
    public static byte[] Decompress(byte[] data)
    {
        // Read data as a stream for simpler reading with the Reader class
        using MemoryStream dataStream = new(data);

        // Read header
        ChunkZipHeader header = new()
        {
            Header = Reader.ReadASCIIStringWithLength(dataStream, _magic.Length),
            VersionNumber = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            FullSize = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            BlockSize = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            NumSegments = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            Alignment = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
        };

        // Read chunks
        using MemoryStream outputStream = new();
        for (int _ = 0; _ < header.NumSegments; _++)
        {
            // Do some weird alignment so that the chunk's data is aligned by 16.
            // This makes the Chunk header not aligned as a small trade off. 
            dataStream.AlignBy(16, 8);

            // Read chunk header
            Chunk chunk = new()
            {
                Size = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
                CompressionType = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            };

            // Read chunk data and put it into a stream in
            // order to use System.IO.Compression.DeflateStream,
            // Then copy it to the output stream.
            byte[] chunkData = Reader.ReadBytes(dataStream, (int)chunk.Size);
            using MemoryStream inputStream = new(chunkData);
            var decompressedStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            decompressedStream.CopyTo(outputStream);
        }

        // return the decompressed data
        outputStream.Position = 0;
        return Reader.ReadBytes(outputStream, (int)outputStream.Length);
    }

    /// <summary>
    /// Compress an array of bytes
    /// </summary>
    public static byte[] Compress(byte[] data)
    {
        using var outputStream = new MemoryStream();

        // Write header
        Writer.WriteBytes(outputStream, [.. _magic]); // magic 
        Writer.WriteUInt32(outputStream, 2, ByteOrder.BigEndian); // version
        Writer.WriteUInt32(outputStream, (uint)data.Length, ByteOrder.BigEndian); // fullSize
        Writer.WriteUInt32(outputStream, DefaultBlockSize, ByteOrder.BigEndian); // blockSize
        Writer.WriteUInt32(outputStream, GetNumberOfSegments(data.Length), ByteOrder.BigEndian); // numSegments
        Writer.WriteUInt32(outputStream, 16, ByteOrder.BigEndian); // alignment

        // Compress data into chunks
        List<byte[]> compressedBlocks = [];
        using (MemoryStream dataStream = new(data))
        {
            for (int _ = 0; _ < GetNumberOfSegments(data.Length); _++)
            {
                int distanceToEndOfData = (int)(dataStream.Length - dataStream.Position);
                byte[] blockData = Reader.ReadBytes(dataStream, Math.Min(DefaultBlockSize, distanceToEndOfData));
                var deflater = new ICSharpCode.SharpZipLib.Zip.Compression.Deflater(
                    6,     // level
                    true   // true = RAW DEFLATE (NO zlib header)
                );

                deflater.SetInput(blockData);
                deflater.Finish();
                var outBuf = new byte[blockData.Length * 2];
                int size = deflater.Deflate(outBuf);

                compressedBlocks.Add(outBuf.Take(size).ToArray());
            }
        }

        // Create the chunks
        foreach (byte[] block in compressedBlocks)
        {
            outputStream.AlignBy(16, 8);
            Writer.WriteUInt32(outputStream, (uint)block.Length, ByteOrder.BigEndian); // size
            Writer.WriteUInt32(outputStream, 1, ByteOrder.BigEndian); // compressionType
            Writer.WriteBytes(outputStream, block); // compressedChunkData
        }

        return outputStream.ToArray();
    }

    private static uint GetNumberOfSegments(int fullSize) => (uint)MathF.Ceiling((float)fullSize / DefaultBlockSize);

    private struct ChunkZipHeader
    {
        public string Header;
        public uint VersionNumber;
        public uint FullSize;
        public uint BlockSize;
        public uint NumSegments;
        public uint Alignment;
    }

    private struct Chunk
    {
        public uint Size;
        public uint CompressionType;
    }
}