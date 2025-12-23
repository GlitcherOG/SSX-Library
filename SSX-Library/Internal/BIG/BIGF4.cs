using System.Collections.Immutable;
using System.ComponentModel;
using SSX_Library.Utilities;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles BIGF and BIG4 type big files.
/// </summary>
public static class BIGF4
{
    private static readonly ImmutableArray<byte> _magicBigF = [0x42, 0x49, 0x47, 0x46];
    private static readonly ImmutableArray<byte> _magicBig4 = [0x42, 0x49, 0x47, 0x34];
    private const int _footerLength = 8;

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    public static List<MemberFileInfo> GetMembersInfo(string bigPath)
    {
        //Open the big file
        using var bigStream = File.OpenRead(bigPath);

        // Confirm magic signature is valid
        byte[] magic = new byte[4];
        bigStream.Read(magic);
        if (!magic.SequenceEqual(_magicBigF) && !magic.SequenceEqual(_magicBig4))
        {
            throw new InvalidDataException("Invalid Big signature.");
        }

        // Read Big Header
        bigStream.Position += 4; // fileSize u32
        var fileCount = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian);
        bigStream.Position += 4; // footerOffset u32

        // Read Member file headers
        List<MemberFileInfo> info = [];
        for (int _ = 0; _ < fileCount; _++)
        {
            bigStream.Position += 4; // offset u24
            MemberFileInfo file = new()
            {
                Size = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
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
        byte[] magic = new byte[4];
        bigStream.Read(magic);
        if (!magic.SequenceEqual(_magicBigF) && !magic.SequenceEqual(_magicBig4))
        {
            throw new InvalidDataException("Invalid Big signature.");
        }

        // Read Big Header
        BIGF4Header header = new()
        {
            FileSize = Reader.ReadUInt32(bigStream, ByteOrder.LittleEndian),
            FileCount = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
            FooterOffset = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
        };

        // Read Member file headers
        List<MemberFileHeader> memberFileHeaders = [];
        for (int _ = 0; _ < header.FileCount; _++)
        {
            MemberFileHeader file = new()
            {
                Offset = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
                Size = Reader.ReadUInt32(bigStream, ByteOrder.BigEndian),
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
            bigStream.Position = memberFileHeader.Offset;
            var RefCheck = Reader.ReadBytes(bigStream, 2);
            if (RefCheck[1] == 0xFB && RefCheck[0] == 0x10) // Refpack flags
            {
                data = RefpackHandler.Decompress(data); 
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
    /// /// <param name="bigType">The signature type for the file. In this case BigF or Big4.</param>
    /// <param name="useCompression">Should compress all files with refpack.</param>
    /// <param name="useBackslashes">Should member files store their paths using backslash.</param>
    public static void Create(BigType bigType, string folderPath, string bigOutputPath, bool useCompression, bool useBackslashes = false)
    {
        using var bigStream = File.Create(bigOutputPath);

        // Magic
        if (bigType == BigType.BIGF)
        {
            Writer.WriteBytes(bigStream, [.._magicBigF]);
        } 
        else if (bigType == BigType.BIG4)
        {
            Writer.WriteBytes(bigStream, [.._magicBig4]);
        }
        else
        {
            throw new InvalidEnumArgumentException("Did not pass BigF or Big4 enum types");
        }

        // Store fileSize position for setting later. Set to zero for now.
        long fileSizePosition = bigStream.Position;
        Writer.WriteUInt32(bigStream, 0, ByteOrder.LittleEndian);

        // Get the path for all the member files relative to the input folder.
        // And replace slashes.
        string[] absoluteFilePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        string[] relativeFilePaths = [.. absoluteFilePaths.Select(path => Path.GetRelativePath(folderPath, path))];

        // Write File count
        int fileCount = relativeFilePaths.Length;
        Writer.WriteUInt32(bigStream, (uint)fileCount, ByteOrder.BigEndian);

        // Store footer position for setting later. Set to zero for now.
        long headerFooterPosition = bigStream.Position;
        Writer.WriteUInt32(bigStream, 0, ByteOrder.BigEndian);

        // Store member file header positions for second pass. Set values to zero for now
        // except for the file paths.
        List<long> memberHeaderPositions = [];
        for (int i = 0; i < fileCount; i++)
        {
            memberHeaderPositions.Add(bigStream.Position);
            Writer.WriteUInt32(bigStream, 0, ByteOrder.BigEndian); // offset
            Writer.WriteUInt32(bigStream, 0, ByteOrder.BigEndian); // size

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
        Writer.WriteUInt32(bigStream, (uint)footerOffset, ByteOrder.BigEndian);
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
            Writer.WriteUInt32(bigStream, (uint)dataOffset, ByteOrder.BigEndian); // offset
            Writer.WriteUInt32(bigStream, (uint)data.Length, ByteOrder.BigEndian); // size
            bigStream.Position = dataEndOffset; // Restore to last position
            Console.WriteLine($"FINISHED {i}/{absoluteFilePaths.Length}");
        }

        // Set file size
        long fileSize = bigStream.Seek(0, SeekOrigin.End);
        bigStream.Position = fileSizePosition;
        Writer.WriteUInt32(bigStream, (uint)fileSize, ByteOrder.LittleEndian);
    }

    private struct BIGF4Header
    {
        public byte[] Magic; // Size 4. Can be BigF or Big4
        public uint FileSize;
        public uint FileCount;
        public uint FooterOffset;  // Relative to this value's end
        public MemberFileHeader[] Files;
    }

    private struct MemberFileHeader
    {
        public uint Offset; // Position of file data
        public uint Size; // Size of file data
        public string Path; // null terminated
    }
}