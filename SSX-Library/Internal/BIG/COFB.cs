// using SSX_Library.Utilities;


using System.Collections.Immutable;
using SSX_Library.Utilities;

namespace SSX_Library.Internal.BIG;

/// <summary>
/// Handles COFB type big files.
/// </summary>
internal static class COFB
{
    private static readonly ImmutableArray<byte> _magic = [0xC0, 0xFB];

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    /// <param name="stream">The file stream to read from.</param>
    public static List<MemberFileInfo> GetMembersInfo(Stream stream)
    {
        // Confirm magic signature is valid
        byte[] magic = new byte[2];
        stream.Read(magic);
        if (magic[0] != _magic[0] || magic[1] != _magic[1])
        {
            throw new InvalidDataException("Invalid C0FB signature.");
        }

        // Read Big Header
        stream.Position += 2; // footerOffset u16
        var fileCount = Reader.ReadUInt16(stream, ByteOrder.BigEndian); // fileCount

        // Read Member file headers
        List<MemberFileInfo> info = [];
        for (int _ = 0; _ < fileCount; _++)
        {
            stream.Position += 3; // offset u24
            MemberFileInfo file = new()
            {
                size = Reader.ReadUInt24(stream, ByteOrder.BigEndian),
                path = Reader.ReadNullTerminatedASCIIString(stream),
            };
            info.Add(file);
        }
        return info;
    }


    /// <summary>
    /// Load Big from a stream.
    /// </summary>
    public void LoadFromStream(Stream stream)
    {
        // Confirm magic signature is valid
        byte[] magic = new byte[2];
        stream.Read(magic);
        if (magic[0] != _magic[0] || magic[1] != _magic[1])
        {
            throw new InvalidDataException("Invalid C0FB signature.");
        }

        // Read Big Header
        _bigHeader = new()
        {
            footerOffset = Reader.ReadUInt16(stream, ByteOrder.BigEndian),
            fileCount = Reader.ReadUInt16(stream, ByteOrder.BigEndian),
        };

        // Read Member file headers
        _memberFilesData = [];
        for (int _ = 0; _ < _bigHeader.fileCount; _++)
        {
            MemberFileHeader file = new()
            {
                offset = Reader.ReadUInt24(stream, ByteOrder.BigEndian),
                size = Reader.ReadUInt24(stream, ByteOrder.BigEndian),
                path = Reader.ReadNullTerminatedASCIIString(stream),
            };
            _memberFiles.Add(file);
        }

        // Read member files data
        _memberFiles = [];
        foreach (var file in _memberFiles)
        {
            stream.Position = file.offset;
            MemberFileData data = new()
            {
                data = Reader.ReadBytes(stream, (int)file.size)
            };
            _memberFilesData.Add(data);
        }
    }

    /// <summary>
    /// Save Big to a stream.
    /// </summary>
    public void SaveToStream(Stream stream)
    {
        
    }

    /// <summary>
    /// Create and load from a folder on disk.
    /// </summary>
    public void CreateFromFolder(string folderPath)
    {
        
    }

    /// <summary>
    /// Extracts member files into the game's folder.
    /// </summary>
    /// <param name="gameRootPath"> The folder path to the game 
    /// (e.g. where the .elf and data folder is)</param>
    public void ExtractToGameFolder(string gameRootPath)
    {
        
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

    private struct MemberFileData
    {
        public byte[] data;
    }


}