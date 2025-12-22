using System.Collections.Immutable;
using SSX_Library.Utilities;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles NewBig type big files.
/// </summary>
public static class NewBig
{
    private static readonly ImmutableArray<byte> _magic = [0x45, 0x42]; // "EB"
    private const int _footerLength = 8;

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    public static List<MemberFileInfo> GetMembersInfo(string bigPath)
    {
        //Open the big file
        using var bigStream = File.OpenRead(bigPath);

        // Confirm magic signature is valid
        byte[] magic = new byte[2];
        bigStream.Read(magic);
        if (!magic.SequenceEqual(_magic))
        {
            throw new InvalidDataException("Invalid NewBig signature.");
        }

        // Read Big Header
        Header header = new()
        {
            Signature = [.. _magic],
            HeaderVersion = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
            FileCount = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            Flags = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
            Aligment = Reader.ReadBytes(bigStream, 1)[0],
            Reserved = Reader.ReadBytes(bigStream, 1)[0],
            BaseHeaderSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            NameHeaderSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            NameLength = Reader.ReadBytes(bigStream, 1)[0],
            PathLength = Reader.ReadBytes(bigStream, 1)[0],
            NumPath = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
            FileSize = Reader.ReadUInt64(bigStream, ByteOrder.BigEndian)
        };

        // Footer
        bigStream.Position += 16;



    }

    private struct Header
    {
        public byte[] Signature;     // "EB" (Electronic Arts Bigfile)
        public ushort HeaderVersion; // version number
        public uint FileCount;       // number of files in this archive
        public ushort Flags;         // bitflags
        public byte Aligment;        // power of two on which the files are aligned (default = 4, 16 bytes)
        public byte Reserved;        // not used
        public uint BaseHeaderSize;  // file header + hash index
        public uint NameHeaderSize;  // size of the names & paths, in bytes
        public byte NameLength;      // length of each name entry
        public byte PathLength;      // length of each path entry
        public ushort NumPath;       // total number of unique paths
        public ulong FileSize;       // total size of the bigfile
    }

    public struct FileIndex
    {
        public int Offset;
        public int zSize; //Uncompressed only used for refpack
        public int Size;
        public int Hash;
    }

    private struct PathEntry
    {
        public int DirectoryIndex;
        public string Filename;
    }
}
    