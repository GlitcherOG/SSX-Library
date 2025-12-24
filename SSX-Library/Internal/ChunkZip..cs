using SSX_Library.Utilities;
using System.Collections.Immutable;
using System.Text;

namespace SSX_Library.Internal;

/// <summary>
/// Helper class for dealing with ChunkZip compression
/// </summary>
internal static class ChunkZip
{
    private static readonly ImmutableArray<byte> _magic = [..Encoding.ASCII.GetBytes("chunkzip")];

    /// <summary>
    /// Peeks at the current stream position to check for a ChunkZip signature, 
    /// restoring the original position before returning.
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
        for (int a = 0; a < header.NumSegments; a++)
        {
            
        }



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

    private struct Chunk
    {
        public uint ChunkSize;
        public uint CompressionType;
    }
}