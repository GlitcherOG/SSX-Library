using System.Collections.Immutable;
using SSX_Library.Internal.Extensions;
using SSX_Library.Utilities;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles NewBig type big files.
/// </summary>
internal static class NewBig
{
    private static readonly ImmutableArray<byte> _magic = [0x45, 0x42]; // "EB"

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    public static List<MemberFileInfo> GetMembersInfo(string bigPath)
    {
        // Load header information for the Big and it's members
        LoadedInformation headerInfo = LoadHeaderInfo(bigPath);

        // Get member file information
        List<MemberFileInfo> fileInfos = [];
        for (int i = 0; i < headerInfo.HashIndices.Count; i++)
        {
            string path = Path.Join(headerInfo.Directories[headerInfo.PathEntries[i].DirectoryIndex], headerInfo.PathEntries[i].Filename);
            
            fileInfos.Add(new MemberFileInfo()
            {
                Path = path,
                Size = headerInfo.HashIndices[i].Size,
            });
        }
        return fileInfos;
    }

    /// <summary>
    /// Extracts member files into a folder.
    /// </summary>
    public static void Extract(string bigPath, string extractionPath)
    {
        // Load header information for the Big and it's members
        LoadedInformation headerInfo = LoadHeaderInfo(bigPath);
        
        //Open the big file
        using var bigStream = File.OpenRead(bigPath);

        // Create each member file
        for (int i = 0; i < headerInfo.HashIndices.Count; i++)
        {
            // Generate the path and directory to put on disk
            string relativeDirectory = headerInfo.Directories[headerInfo.PathEntries[i].DirectoryIndex];
            string filename = headerInfo.PathEntries[i].Filename;
            string absoluteDirectory = Path.Join(extractionPath, relativeDirectory).Replace('\\', '/');
            string absolutePath = Path.Join(absoluteDirectory, filename).Replace('\\', '/');

            // Create directory if it doesnt exist
            if (!Directory.Exists(absoluteDirectory))
            {
                Directory.CreateDirectory(absoluteDirectory);
            }

            // Create file
            using var outputStream = File.Create(absolutePath);

            // Read the member file data, decompress it if needed,
            // and write it to the output file.
            bigStream.Position = headerInfo.HashIndices[i].Offset * 16;
            if (RefpackHandler.HasRefpackSignature(bigStream))
            {
                byte[] data = Reader.ReadBytes(bigStream, (int)headerInfo.HashIndices[i].zSize);
                Writer.WriteBytes(outputStream, RefpackHandler.Decompress(data));
            }
            else if (ChunkZip.HasChunkZipSignature(bigStream))
            {
                byte[] data = Reader.ReadBytes(bigStream, (int)headerInfo.HashIndices[i].Size);
                Writer.WriteBytes(outputStream, ChunkZip.Decompress(data));
            }
            else
            {
                // No compression. Write raw data.
                byte[] data = Reader.ReadBytes(bigStream, (int)headerInfo.HashIndices[i].Size);
                Writer.WriteBytes(outputStream, data);
            }
        }
    }

    /// <summary>
    /// Create a NewBig file from a folder.
    /// </summary>
    /// <param name="useCompression">Should compress all files with refpack.</param>
    public static void Create(string folderPath, string bigOutputPath, bool useCompression)
    {
        using var bigStream = File.Create(bigOutputPath);
        string[] filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        
        // Skip header and FileIndices for now
        bigStream.Position = 48;
        bigStream.Position += 16 * filePaths.Length;
        
        int longestNameLength = 0;
        int longestDirectoryLength = 0;
        List<string> directories = [];
        List<FileIndex> filesIndices = [];
        List<FileIndexData> filesIndicesData = [];
        Header header = new();
        foreach (string filePath in filePaths)
        {
            string name = Path.GetFileName(filePath);
            string directory = Path.GetDirectoryName(filePath) ?? "";
            
            // Get the size of the largest filename and directory string.
            if (longestNameLength < name.Length)
            {
                longestNameLength = name.Length + 2;
            }
            if (longestDirectoryLength < directory.Length)
            {
                longestDirectoryLength = directory.Length + 1;
            }

            // Create the array of directories.
            if (!directories.Contains(directory))
            {
                directories.Add(directory);
            }

            // And Create FileIndices and FileData for the member files.
            FileIndex fileIndex = new()
            {
                Hash = Hash(filePath),
            };
            filesIndices.Add(fileIndex);
            FileIndexData fileIndexData = new()
            {
                DirectoryIndex = (ushort)directories.IndexOf(directory),
                Filename = name,
            };
            filesIndicesData.Add(fileIndexData);
        }

        //Gap is file count alligned by 16
        bigStream.Position += filePaths.Length;
        bigStream.AlignBy16();

        long baseHeaderSize = bigStream.Position;

        //Make Space for paths
        long tempNameLength = bigStream.Position;
        bigStream.Position += (longestNameLength + 2) * filePaths.Length;
        bigStream.AlignBy16();
        bigStream.Position += longestDirectoryLength * filePaths.Length;
        bigStream.AlignBy16();







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
            Aligment = Reader.ReadByte(bigStream),
            Reserved = Reader.ReadByte(bigStream),
            BaseHeaderSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            NameHeaderSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            PathEntrySize = Reader.ReadByte(bigStream),
            DirectoryEntrySize = Reader.ReadByte(bigStream),
            UniqueDirectoryEntryCount = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
            FileSize = Reader.ReadUInt64(bigStream, ByteOrder.BigEndian)
        };

        // Padding
        bigStream.Position += 16;

        // Read Hash Indices
        List<HashIndex> hashIndices = [];
        for (int i = 0; i < header.FileCount; i++)
        {
            HashIndex fileIndex = new()
            {
                Offset = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                zSize = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                Size = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                Hash = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian)
            };
            hashIndices.Add(fileIndex);
        }

        // Read path entries
        bigStream.Position = header.BaseHeaderSize;
        List<PathEntry> pathEntries = [];
        for (int i = 0; i < header.FileCount; i++)
        {
            PathEntry pathEntry = new()
            {
                DirectoryIndex = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
                Filename = Reader.ReadNullTerminatedASCIIString(bigStream),
            };
            pathEntries.Add(pathEntry);
        }

       // Read directories
        bigStream.AlignBy16();
        List<string> directories = [];
        for (int i = 0; i < header.UniqueDirectoryEntryCount; i++)
        {
            directories.Add(Reader.ReadNullTerminatedASCIIString(bigStream));
        }

        return new LoadedInformation()
        {
            BigHeader = header,
            HashIndices = hashIndices,
            PathEntries = pathEntries,
            Directories = directories,
        };
    }

    /// <summary>
    /// djb2 algorithm by Daniel J. Bernstein
    /// </summary>
    private static uint Hash(string str) 
    {
        uint hash = 5381;
        foreach (char character in str)
        {
            hash = (hash * 33) + character;
        }
        return hash;
    }

    private struct LoadedInformation
    {
        public Header BigHeader;
        public List<HashIndex> HashIndices;
        public List<PathEntry> PathEntries;
        public List<string> Directories;
    }

    /// <remarks>
    /// NameLength is actually the size of FileIndexData, not the filename alone.
    /// </remarks>
    private struct Header
    {
        public byte[] Signature; // "EB" (Electronic Arts Bigfile)
        public ushort HeaderVersion; // version number
        public uint FileCount; // number of files in this archive
        public ushort Flags; // bitflags
        public byte Aligment; // power of two on which the files are aligned (default = 4, 16 bytes)
        public byte Reserved; // not used
        public uint BaseHeaderSize; // file header + hash index + index flags
        public uint NameHeaderSize; // size of the Path and Directory entries, in bytes
        public byte PathEntrySize; // size of each PathEntry in bytes
        public byte DirectoryEntrySize; // size of each DirectoryEntry in bytes
        public ushort UniqueDirectoryEntryCount; // total number of unique directories
        public ulong FileSize; // total size of the bigfile
    }

    private struct HashIndex
    {
        // offset divided by the alignment to the start of the file.
        // To get to the file location, multiply this by the alignment (16 by default)
        public uint Offset;
        public uint zSize; // size of the file data if its Refpack compressed
        public uint Size; // size of the file data if its Chunkzip compressed or uncompressed
        public uint Hash; // 32-bit DJB2 hash
    }

    /// <remarks>
    /// The size of the whole struct is always Header.PathEntrySize.
    /// </remarks>
    private struct PathEntry
    {
        public ushort DirectoryIndex; // i.e. an index to List<string> Paths; from LoadedInformation
        public string Filename;
    }
}
