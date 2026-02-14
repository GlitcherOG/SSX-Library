using System.Collections.Immutable;
using SSX_Library.Internal.Utilities;
using SSXLibrary.FileHandlers;
using SSX_Library.Internal.Extensions;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles COFB type big files.
/// </summary>
internal static class COFB
{
    private static readonly ImmutableArray<byte> _magic = [0xC0, 0xFB];

    public static bool IsStreamCOFB(Stream stream)
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
        //Open the big file
        using var bigStream = File.OpenRead(bigPath);

        // Confirm magic signature is valid
        byte[] magic = new byte[2];
        bigStream.Read(magic);
        if (magic[0] != _magic[0] || magic[1] != _magic[1])
        {
            throw new InvalidDataException("Invalid C0FB signature.");
        }

        // Read Big Header
        bigStream.Position += 2; // headerSize u16
        var fileCount = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian);
        List<MemberFileInfo> info = [];
        for (int _ = 0; _ < fileCount; _++)
        {
            bigStream.Position += 3; // offset u24
            MemberFileInfo file = new()
            {
                Size = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                Path = Reader.ReadNullTerminatedASCIIString(bigStream),
            };
            info.Add(file);
        }
        return info;
    }

    /// <summary>
    /// Extracts member files into a folder.
    /// </summary>
    public static void Extract(string bigPath, string extractionPath)
    {
        //Open the big file
        using var bigStream = File.OpenRead(bigPath);

        // Confirm magic signature is valid
        byte[] magic = new byte[2];
        bigStream.Read(magic);
        if (magic[0] != _magic[0] || magic[1] != _magic[1])
        {
            throw new InvalidDataException("Invalid C0FB signature.");
        }

        // Read Big Header
        bigStream.Position += 2; // headerSize u16
        uint fileCount = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian);
        List<MemberFileHeader> memberFileHeaders = [];
        for (int _ = 0; _ < fileCount; _++)
        {
            MemberFileHeader file = new()
            {
                Offset = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                Size = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                Path = Reader.ReadNullTerminatedASCIIString(bigStream),
            };
            memberFileHeaders.Add(file);
        }

        // Read and create member files
        foreach (var memberFileHeader in memberFileHeaders)
        {
            // Validate files
            if (memberFileHeader.Offset == 0 || memberFileHeader.Path.Contains('*')) continue;

            // Read memberFileHeader data
            bigStream.Position = memberFileHeader.Offset;
            byte[] data = Reader.ReadBytes(bigStream, (int)memberFileHeader.Size);

            // Check if compressed. If so then decompress
            if (data[1] == 0xFB && data[0] == 0x10) // Refpack flags
            {
                data = Refpack.Decompress(data); 
            }

            // Create file
            string combinedPath = Path.Join(extractionPath, memberFileHeader.Path).Replace('\\', '/');
            if (!Directory.Exists(Path.GetDirectoryName(combinedPath) ?? ""))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(combinedPath) ?? "");
            }
            var file = File.Create(combinedPath);
            file.Write(data);
            file.Close();
        }
    }

    /// <summary>
    /// Create a big file from a folder.
    /// </summary>
    /// <param name="useCompression">Should compress all files with refpack.</param>
    /// <param name="useBackslashes">Should member files store their paths using backslash.</param>
    public static void Create(string folderPath, string bigOutputPath, bool useCompression, bool useBackslashes = false)
    {
        using var bigStream = File.Create(bigOutputPath);

        // Magic
        Writer.WriteBytes(bigStream, [.._magic]);

        // Set header size to zero for now.
        Writer.WriteUInt16(bigStream, 0, ByteOrder.BigEndian);

        // Get the path for all the member files relative to the input folder.
        // And replace slashes.
        string[] absoluteFilePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        string[] relativeFilePaths = [.. absoluteFilePaths.Select(path => Path.GetRelativePath(folderPath, path))];

        // Write File count
        int fileCount = relativeFilePaths.Length;
        Writer.WriteUInt16(bigStream, (ushort)fileCount, ByteOrder.BigEndian);

        // Store member file header offsets and size for second pass. Set values to zero for now
        // except for the file paths.
        List<long> memberHeaderOffsets = [];
        for (int i = 0; i < fileCount; i++)
        {
            memberHeaderOffsets.Add(bigStream.Position);
            Writer.WriteUInt24(bigStream, 0, ByteOrder.BigEndian); // offset
            Writer.WriteUInt24(bigStream, 0, ByteOrder.BigEndian); // size

            // Select slash type
            string path = useBackslashes switch
            {
                true => relativeFilePaths[i].Replace('/', '\\'),
                false => relativeFilePaths[i].Replace('\\', '/'),
            };
            Writer.WriteNullTerminatedASCIIString(bigStream, path);
        }

        // Rewrite the header size
        long headerSize = bigStream.Position;
        bigStream.Position = 2; // header size
        Writer.WriteUInt16(bigStream, (ushort)headerSize, ByteOrder.BigEndian);
        bigStream.Position = headerSize; // Restore to last position

        // Write file data
        for (int i = 0; i < absoluteFilePaths.Length; i++)
        {
            string path = absoluteFilePaths[i];
            byte[] data = File.ReadAllBytes(path);
            if (useCompression)
            {
                data = Refpack.Compress(data);
            }
            bigStream.AlignBy(128);
            long dataOffset = bigStream.Position;
            Writer.WriteBytes(bigStream, data);

            // Update header
            long dataEndOffset = bigStream.Position;
            bigStream.Position = memberHeaderOffsets[i];
            Writer.WriteUInt24(bigStream, (uint)dataOffset, ByteOrder.BigEndian); // offset
            Writer.WriteUInt24(bigStream, (uint)data.Length, ByteOrder.BigEndian); // size
            bigStream.Position = dataEndOffset; // Restore to last position
        }
    }

    private struct MemberFileHeader
    {
        public uint Offset; // Position of file data
        public uint Size; // Size of file data
        public string Path; // null terminated
    }
}