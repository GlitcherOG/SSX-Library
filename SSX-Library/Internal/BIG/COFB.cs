using System.Collections.Immutable;
using SSX_Library.Utilities;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles COFB type big files.
/// </summary>
public static class COFB
{
    private static readonly ImmutableArray<byte> _magic = [0xC0, 0xFB];
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
        if (magic[0] != _magic[0] || magic[1] != _magic[1])
        {
            throw new InvalidDataException("Invalid C0FB signature.");
        }

        // Read Big Header
        bigStream.Position += 2; // footerOffset u16
        var fileCount = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian); // fileCount

        // Read Member file headers
        List<MemberFileInfo> info = [];
        for (int _ = 0; _ < fileCount; _++)
        {
            bigStream.Position += 3; // offset u24
            MemberFileInfo file = new()
            {
                size = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                path = Reader.ReadNullTerminatedASCIIString(bigStream),
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
        COFBHeader header = new()
        {
            footerOffset = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
            fileCount = Reader.ReadUInt16(bigStream, ByteOrder.BigEndian),
        };

        // Read Member file headers
        List<MemberFileHeader> memberFileHeaders = [];
        for (int _ = 0; _ < header.fileCount; _++)
        {
            MemberFileHeader file = new()
            {
                offset = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                size = Reader.ReadUInt24(bigStream, ByteOrder.BigEndian),
                path = Reader.ReadNullTerminatedASCIIString(bigStream),
            };
            memberFileHeaders.Add(file);
        }

        // Read and create member files
        foreach (var memberFileHeader in memberFileHeaders)
        {
            // Validate files
            if (memberFileHeader.offset == 0 || memberFileHeader.path.Contains('*')) continue;

            // Read memberFileHeader data
            bigStream.Position = memberFileHeader.offset;
            byte[] data = Reader.ReadBytes(bigStream, (int)memberFileHeader.size);

            // Check if compressed. If so then decompress
            bigStream.Position = memberFileHeader.offset;
            var RefCheck = Reader.ReadBytes(bigStream, 2);
            if (RefCheck[1] != 0xFB || RefCheck[0] != 0x10) // Refpack flags
            {
                data = RefpackHandler.Decompress(data); 
            }

            // Create file
            string combinedPath = Path.Join(extractionPath, memberFileHeader.path).Replace('\\', '/');
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

        // Store footer position for setting later. Set to zero for now.
        long headerFooterPosition = bigStream.Position;
        Writer.WriteUInt16(bigStream, 0, ByteOrder.BigEndian);

        // Get the path for all the member files relative to the input folder.
        // And replace slashes.
        string[] absoluteFilePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        string[] relativeFilePaths = [.. absoluteFilePaths.Select(path => Path.GetRelativePath(folderPath, path))];

        // Write File count
        int fileCount = relativeFilePaths.Length;
        Writer.WriteUInt16(bigStream, (ushort)fileCount, ByteOrder.BigEndian);

        // Store member file header positions for second pass. Set values to zero for now
        // except for the file paths.
        List<long> memberHeaderPositions = [];
        for (int i = 0; i < fileCount; i++)
        {
            memberHeaderPositions.Add(bigStream.Position);
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

        // Write Footer offset to the header and write the footer
        long footerOffset = bigStream.Position;
        bigStream.Position = headerFooterPosition;
        Writer.WriteUInt16(bigStream, (ushort)footerOffset, ByteOrder.BigEndian);
        bigStream.Position = footerOffset; // Restore back to footer position
        Writer.WriteBytes(bigStream, new byte[_footerLength]);

        // Write file data
        for (int i = 0; i < absoluteFilePaths.Length; i++)
        {
            string path = absoluteFilePaths[i];
            byte[] data = File.ReadAllBytes(path);
            if (useCompression)
            {
                RefpackHandler.Compress(data, out data, CompressionLevel.Max);
            }
            long dataOffset = bigStream.Position;
            Writer.WriteBytes(bigStream, data);

            // Update header
            long dataEndOffset = bigStream.Position;
            bigStream.Position = memberHeaderPositions[i];
            Writer.WriteUInt24(bigStream, (uint)dataOffset, ByteOrder.BigEndian); // offset
            Writer.WriteUInt24(bigStream, (uint)data.Length, ByteOrder.BigEndian); // size
            bigStream.Position = dataEndOffset; // Restore to last position
        }
    }

    public struct MemberFileInfo
    {
        public string path;
        public uint size;
    }

    private struct COFBHeader
    {
        public byte[] magic; // Size 2
        public uint footerOffset;  // Relative to this value's end
        public uint fileCount;
        public MemberFileHeader[] files;
    }

    private struct MemberFileHeader
    {
        public uint offset; // Position of file data
        public uint size; // Size of file data
        public string path; // null terminated
    }
}