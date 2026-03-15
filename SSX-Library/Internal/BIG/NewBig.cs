using System.Collections.Immutable;
using SSX_Library.Internal.Utilities.StreamExtensions;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles NewBig type big files.
/// </summary>
internal static class NewBig
{
    private static readonly ImmutableArray<byte> _magic = [0x45, 0x42]; // "EB"

    public static bool IsStreamNewBig(Stream stream)
    {
        byte[] magic = new byte[_magic.Length];
        stream.Read(magic);
        stream.Position = 0;
        return magic.SequenceEqual(_magic);
    }

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
            fileInfos.Add(new()
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
            if (Refpack.HasRefpackSignature(bigStream))
            {
                byte[] data = bigStream.ReadBytes((int)headerInfo.HashIndices[i].zSize);
                outputStream.Write(Refpack.Decompress(data));
            }
            else if (ChunkZip.HasChunkZipSignature(bigStream))
            {
                byte[] data = bigStream.ReadBytes((int)headerInfo.HashIndices[i].Size);
                outputStream.Write(ChunkZip.Decompress(data));
            }
            else
            {
                // No compression. Write raw data.
                byte[] data = bigStream.ReadBytes((int)headerInfo.HashIndices[i].Size);
                outputStream.Write(data);
            }
        }
    }

    /// <summary>
    /// Create a NewBig file from a folder.
    /// </summary>
    /// <param name="useCompression">Should compress all files with ChunkZip.</param>
    public static void Create(string folderPath, string bigOutputPath, bool useCompression)
    {
        using var bigStream = File.Create(bigOutputPath);

        // Get the path for all the member files relative to the input folder.
        // And replace slashes.
        string[] absoluteFilePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        string[] relativeFilePaths = [.. absoluteFilePaths.Select(path => Path.GetRelativePath(folderPath, path))];

        // Skip header and HashIndices for now
        bigStream.Position = 48;
        bigStream.Position += 16 * relativeFilePaths.Length;

        // Write flags. Set all to zero.
        bigStream.Write(new byte[relativeFilePaths.Length]); 
        bigStream.AlignBy16();
        long baseHeaderSize = bigStream.Position;

        int longestDirectoryStringLength = 1; // Including null character
        int longestFilenameStringLength = 1; // Including null character
        List<string> uniqueDirectories = []; // Excludes null character
        List<HashPathBundle> hashPathBundles = [];
        for (int i = 0; i < absoluteFilePaths.Length; i++)
        {
            string absolutePath = absoluteFilePaths[i];
            string relativePath = relativeFilePaths[i];
            string name = Path.GetFileName(relativePath);
            string? directory = Path.GetDirectoryName(relativePath) ?? throw new InvalidDataException($"File path {relativePath} is invalid.");

            // Get every unique directory, put it on a list,
            if (!uniqueDirectories.Contains(directory))
            {
                uniqueDirectories.Add(directory);
            }

            // Get the longest directory string length.
            if (directory.Length + 1 > longestDirectoryStringLength)
            {
                longestDirectoryStringLength = directory.Length + 1;
            }

            // Get the longest filename string length.
            if (name.Length + 1 > longestFilenameStringLength)
            {
                longestFilenameStringLength = name.Length + 1;
            }

            // Create a bundle
            HashPathBundle bundle = new()
            {
                HashIndexField = new()
                {
                    Hash = HashAlgo(directory + "/" + name)      
                },
                PathEntryField = new()
                {
                    DirectoryIndex = (ushort)uniqueDirectories.IndexOf(directory),
                    Filename = name, // Excludes null character
                },
                AbsolutePath = absolutePath,
            };
            hashPathBundles.Add(bundle);
        }

        // Sort the bundles based on hash number
        hashPathBundles.Sort((x, y) => x.HashIndexField.Hash.CompareTo(y.HashIndexField.Hash));

        // Write PatchEntries
        foreach (HashPathBundle bundle in hashPathBundles)
        {
            bigStream.WriteUInt16(bundle.PathEntryField.DirectoryIndex, ByteOrder.BigEndian);
            bigStream.WriteAsciiNullTerminated(bundle.PathEntryField.Filename);
            int stringSizeWithNull = bundle.PathEntryField.Filename.Length + 1;
            if (stringSizeWithNull < longestFilenameStringLength)
            {
                // Padding
                bigStream.Position += longestFilenameStringLength - stringSizeWithNull;
            }
        }
        bigStream.AlignBy16();

        // Write Directory entries
        foreach (string directory in uniqueDirectories)
        {
            bigStream.WriteAsciiNullTerminated(directory);
            int stringSizeWithNull = directory.Length + 1;
            if (stringSizeWithNull < longestDirectoryStringLength)
            {
                // Padding
                bigStream.Position += longestDirectoryStringLength - stringSizeWithNull;
            }
        }
        bigStream.AlignBy16();
        long nameHeaderSize = bigStream.Position - baseHeaderSize;

        // Write HashIndices and file data
        for (int i = 0; i < hashPathBundles.Count; i++)
        {
            const long HashIndicesPosition = 0x30;
            byte[] data = File.ReadAllBytes(hashPathBundles[i].AbsolutePath);
            if (useCompression)
            {
                data = ChunkZip.Compress(data);
            }
            long endOfStream = bigStream.Position;
            uint dataOffset = (uint)endOfStream / 16;

            // Update the hash index placeholder
            bigStream.Position = HashIndicesPosition + (i * 16);
            bigStream.WriteUInt32(dataOffset, ByteOrder.BigEndian); // Offset
            bigStream.WriteUInt32(0, ByteOrder.BigEndian); // zSize
            bigStream.WriteUInt32((uint)data.Length, ByteOrder.BigEndian); // Sizze
            bigStream.WriteUInt32(hashPathBundles[i].HashIndexField.Hash, ByteOrder.BigEndian); // Hash

            // Write the data
            bigStream.Position = endOfStream;
            bigStream.Write(data);
            bigStream.AlignBy16();
        }

        // Update the header placeholder
        bigStream.Position = 0;
        bigStream.Write([.. _magic]);
        bigStream.WriteUInt16(3, ByteOrder.BigEndian); // version
        bigStream.WriteUInt32((uint)hashPathBundles.Count, ByteOrder.BigEndian); // fileCount
        bigStream.WriteUInt16(16, ByteOrder.BigEndian); // flags
        bigStream.WriteByte(4); // alignment
        bigStream.WriteByte(0); // reserved
        bigStream.WriteUInt32((uint)baseHeaderSize, ByteOrder.BigEndian); // baseHeaderSize
        bigStream.WriteUInt32((uint)nameHeaderSize, ByteOrder.BigEndian); // nameHeaderSize
        bigStream.WriteByte((byte)(longestFilenameStringLength + 2)); // pathEntrySize
        bigStream.WriteByte((byte)longestDirectoryStringLength); // directoryEntrySize
        bigStream.WriteUInt16((ushort)uniqueDirectories.Count, ByteOrder.BigEndian); // fileSize
        bigStream.WriteUInt64((ulong)bigStream.Length, ByteOrder.BigEndian); // fileSize
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
            HeaderVersion = bigStream.ReadUInt16(ByteOrder.BigEndian),
            FileCount = bigStream.ReadUInt32(ByteOrder.BigEndian),
            Flags = bigStream.ReadUInt16(ByteOrder.BigEndian),
            Aligment = (byte)bigStream.ReadByte(),
            Reserved = (byte)bigStream.ReadByte(),
            BaseHeaderSize = bigStream.ReadUInt32(ByteOrder.BigEndian),
            NameHeaderSize = bigStream.ReadUInt32(ByteOrder.BigEndian),
            PathEntrySize = (byte)bigStream.ReadByte(),
            DirectoryEntrySize = (byte)bigStream.ReadByte(),
            UniqueDirectoryEntryCount = bigStream.ReadUInt16(ByteOrder.BigEndian),
            FileSize = bigStream.ReadUInt64(ByteOrder.BigEndian)
        };

        // Padding
        bigStream.Position += 16;

        // Read Hash Indices
        List<HashIndex> hashIndices = [];
        for (int i = 0; i < header.FileCount; i++)
        {
            hashIndices.Add(new()
            {
                Offset = bigStream.ReadUInt32(ByteOrder.BigEndian),
                zSize = bigStream.ReadUInt32(ByteOrder.BigEndian),
                Size = bigStream.ReadUInt32(ByteOrder.BigEndian),
                Hash = bigStream.ReadUInt32(ByteOrder.BigEndian)
            });
        }

        // Read path entries
        bigStream.Position = header.BaseHeaderSize;
        List<PathEntry> pathEntries = [];
        for (int i = 0; i < header.FileCount; i++)
        {
            pathEntries.Add(new()
            {
                DirectoryIndex = bigStream.ReadUInt16(ByteOrder.BigEndian),
                Filename = bigStream.ReadAsciiWithLength(header.PathEntrySize - 2, true),
            });
        }
        bigStream.AlignBy16();

       // Read directories
        List<string> directories = [];
        for (int i = 0; i < header.UniqueDirectoryEntryCount; i++)
        {
            directories.Add(bigStream.ReadAsciiWithLength(header.DirectoryEntrySize, true));
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
    private static uint HashAlgo(string str) 
    {
        uint hash = 5381;
        foreach (char character in str)
        {
            hash = (hash * 33) + character;
        }
        return hash;
    }

    /// <remarks>
    /// Bundles HashIndex and PathEntry for sorting
    /// </remarks>
    private struct HashPathBundle
    {
        public HashIndex HashIndexField;
        public PathEntry PathEntryField;
        public string AbsolutePath;
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
        public ushort DirectoryIndex;
        public string Filename;
    }
}
