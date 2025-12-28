using SSX_Library.Internal.Extensions;
using SSX_Library.Utilities;
using System.Collections.Immutable;
using System.Text;
using System.IO.Compression;

namespace SSX_Library.Internal;

/// <summary>
/// Helper class for dealing with ChunkZip compression
/// </summary>
internal static class ChunkZip
{
    private static readonly ImmutableArray<byte> _magic = [..Encoding.ASCII.GetBytes("chunkzip")];

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
        for (int a = 0; a < header.NumSegments; a++)
        {
            // Do some weird alignment so that the chunk's data is aligned by 16.
            // This makes the Chunk header not aligned as a small trade off. 
            dataStream.AlignBy(16, 8);

            // Read chunk header
            ChunkHeader chunkHeader = new()
            {
                Size = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
                CompressionType = Reader.ReadUInt32(dataStream, ByteOrder.BigEndian),
            };
            
            // Read chunk data and put it into a stream in
            // order to use System.IO.Compression.DeflateStream,
            // Then copy it to the output stream.
            byte[] chunkData = Reader.ReadBytes(dataStream, (int)chunkHeader.Size);
            using MemoryStream inputStream = new(chunkData);
            var decompressedStream = new DeflateStream(inputStream, CompressionMode.Decompress);
            decompressedStream.CopyTo(outputStream);
        }

        // return the decompressed data
        outputStream.Position = 0;
        return Reader.ReadBytes(outputStream, (int)outputStream.Length);
    }

    private struct ChunkZipHeader
    {
        public string Header;
        public uint VersionNumber;
        public uint FullSize;
        public uint BlockSize;
        public uint NumSegments; 
        public uint Alignment;
    }

    private struct ChunkHeader
    {
        public uint Size;
        public uint CompressionType;
    }
}