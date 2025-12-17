// using SSX_Library.Utilities;


using System.Collections.Immutable;
using SSX_Library.Utilities;

namespace SSX_Library.Internal.BIG;


/// <summary>
/// Handles COFB type big files.
/// </summary>
internal sealed class COFB
{
    private readonly ImmutableArray<byte> _magic = [0xC0, 0xFB];
    private COFBHeader _bigHeader;
    private List<MemberFileHeader> _memberFiles = [];
    private List<MemberFileData> _memberFilesData = [];

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

    public void LoadFromFolder()
    {
        
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