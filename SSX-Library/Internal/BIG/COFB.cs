// using SSX_Library.Utilities;


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
        long footerPosition = bigStream.Position;
        Writer.WriteUInt16(bigStream, 0, ByteOrder.BigEndian);

        var sus = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        foreach (var file in sus)
        {
            Console.WriteLine(file);
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