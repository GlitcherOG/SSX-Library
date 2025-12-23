using System.Collections.Immutable;
using SSX_Library.Internal.Extensions;
using SSX_Library.Utilities;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles NewBig type big files.
/// </summary>
public static class NewBig
{
    private static readonly ImmutableArray<byte> _magic = [0x45, 0x42]; // "EB"

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    public static List<MemberFileInfo> GetMembersInfo(string bigPath)
    {
        // Load header information
        LoadedInformation headerInfo = LoadHeaderInfo(bigPath);

        // Get member file information
        List<MemberFileInfo> fileInfos = [];
        for (int i = 0; i < headerInfo.FileIndices.Count; i++)
        {
            string path = Path.Join(headerInfo.Paths[headerInfo.PathEntries[i].DirectoryIndex], headerInfo.PathEntries[i].Filename);
            MemberFileInfo fileInfo = new()
            {
                Path = path,
                Size = headerInfo.FileIndices[i].Size,
            };
            fileInfos.Add(fileInfo);
        }
        return fileInfos;
    }

    /// <summary>
    /// Extracts member files into a folder.
    /// </summary>
    public static void Extract(string bigPath, string extractionPath)
    {
        // Load header information
        LoadedInformation headerInfo = LoadHeaderInfo(bigPath);
    }

    private static LoadedInformation LoadHeaderInfo(string bigPath)
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

        // Read File Indices
        List<FileIndex> files = [];
        for (int i = 0; i < header.FileCount; i++)
        {
            FileIndex fileIndex = new()
            {
                Offset = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                zSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                Size = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                Hash = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian)
            };
            files.Add(fileIndex);
        }

        // Read file names and paths
        bigStream.Position = header.BaseHeaderSize;
        List<PathEntry> pathEntries = [];
        for (int i = 0; i < files.Count; i++)
        {
            PathEntry pathEntry = new()
            {
                DirectoryIndex = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
                Filename = Reader.ReadASCIIStringWithLength(bigStream, header.NameLength-2)
            };
            pathEntries.Add(pathEntry);
        }
        bigStream.AlignBy(16);
        List<string> paths = [];
        for (int i = 0; i < header.NumPath; i++)
        {
            paths.Add(Reader.ReadASCIIStringWithLength(bigStream, header.PathLength));
        }

        LoadedInformation information = new()
        {
            BigHeader = header,
            FileIndices = files,
            PathEntries = pathEntries,
            Paths = paths,
        };
        return information;
    }

    private struct LoadedInformation
    {
        public Header BigHeader;
        public List<FileIndex> FileIndices;
        public List<PathEntry> PathEntries;
        public List<string> Paths;
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
        public uint Offset;
        public uint zSize; //Uncompressed only used for refpack
        public uint Size;
        public uint Hash;
    }

    private struct PathEntry
    {
        public int DirectoryIndex;
        public string Filename;
    }
}
    