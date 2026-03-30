using SSX_Library.Internal.Utilities.StreamExtensions;
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
        byte[] buf = stream.ReadBytes(_magic.Length);
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
            Header = dataStream.ReadAsciiWithLength(_magic.Length, false),
            VersionNumber = dataStream.ReadUInt32(ByteOrder.BigEndian),
            FullSize = dataStream.ReadUInt32(ByteOrder.BigEndian),
            BlockSize = dataStream.ReadUInt32(ByteOrder.BigEndian),
            NumSegments = dataStream.ReadUInt32(ByteOrder.BigEndian),
            Alignment = dataStream.ReadUInt32(ByteOrder.BigEndian),
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
                Size = dataStream.ReadUInt32(ByteOrder.BigEndian),
                CompressionType = dataStream.ReadUInt32(ByteOrder.BigEndian),
            };

            // Read chunk data and put it into a stream in
            // order to use System.IO.Compression.DeflateStream,
            // Then copy it to the output stream.
            byte[] chunkData = dataStream.ReadBytes((int)chunk.Size);
            using MemoryStream inputStream = new(chunkData);
            var decompressedStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            decompressedStream.CopyTo(outputStream);
        }

        // return the decompressed data
        outputStream.Position = 0;
        return outputStream.ReadBytes((int)outputStream.Length);
    }

    /// <summary>
    /// Compress an array of bytes
    /// </summary>
    public static byte[] Compress(byte[] data)
    {
        using var outputStream = new MemoryStream();

        // Write header
        outputStream.Write([.. _magic]); // magic 
        outputStream.WriteUInt32(2, ByteOrder.BigEndian); // version
        outputStream.WriteUInt32((uint)data.Length, ByteOrder.BigEndian); // fullSize
        outputStream.WriteUInt32(DefaultBlockSize, ByteOrder.BigEndian); // blockSize
        outputStream.WriteUInt32(GetNumberOfSegments(data.Length), ByteOrder.BigEndian); // numSegments
        outputStream.WriteUInt32(16, ByteOrder.BigEndian); // alignment

        // Compress data into chunks
        List<byte[]> compressedBlocks = [];
        using (MemoryStream dataStream = new(data))
        {
            for (int _ = 0; _ < GetNumberOfSegments(data.Length); _++)
            {
                int distanceToEndOfData = (int)(dataStream.Length - dataStream.Position);
                byte[] blockData = dataStream.ReadBytes(Math.Min(DefaultBlockSize, distanceToEndOfData));
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
            outputStream.WriteUInt32((uint)block.Length, ByteOrder.BigEndian); // size
            outputStream.WriteUInt32(1, ByteOrder.BigEndian); // compressionType
            outputStream.Write(block); // compressedChunkData
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