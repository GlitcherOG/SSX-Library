using SSX_Library.Internal.Utilities.StreamExtensions;
using SSX_Library.Internal.Utilities;

namespace SSX_Library.Internal.Audio;

internal sealed class HDR
{
    public short Unknown1; // U1
    public short Unknown2; // U2
    public ushort EntryTypes;
    public byte FileCount;
    public byte PaddingCount;
    public byte AligmentSize;
    public short Unknown3; // U5
    public int GapSize;
    public byte[] Unknown4 = [];
    public List<FileHeader> FileHeaders = [];
    public byte[] Padding = [];

    public void Load(string path)
    {
        using var stream = File.OpenRead(path);

        Unknown1 = stream.ReadInt16(ByteOrder.LittleEndian);
        Unknown2 = stream.ReadInt16(ByteOrder.LittleEndian); //Always -1
        EntryTypes = (byte)stream.ReadByte();
        FileCount = (byte)stream.ReadByte();
        PaddingCount = (byte)stream.ReadByte();
        AligmentSize = (byte)stream.ReadByte(); //Multi 0 == 1
        Unknown3 = stream.ReadInt16(ByteOrder.LittleEndian);

        stream.Position += EntryTypes switch
        {
            0 or 2 => 2,
            1 or 3 => 1,
            _ => 0,
        };

        FileHeaders = [];
        for (int _ = 0; _ < FileCount; _++)
        {
            FileHeaders.Add(
                EntryTypes switch
                {
                    0 => new()
                    {
                        Offset = stream.ReadInt16(ByteOrder.BigEndian),
                    },
                    1 => new()
                    {
                        Unknown = (byte)stream.ReadByte(),
                        Offset = stream.ReadInt16(ByteOrder.BigEndian),
                    },
                    2 => new()
                    {
                        Offset = stream.ReadInt16(ByteOrder.BigEndian),
                        SpeakerID = (byte)stream.ReadByte(),
                        EventID = (byte)stream.ReadByte(),
                    },
                    3 => new()
                    {
                        Offset = (int)stream.ReadUInt24(ByteOrder.BigEndian),
                        SpeakerID = (byte)stream.ReadByte(),
                        EventID = (byte)stream.ReadByte(),
                    },
                    4 => new()
                    {
                        Unknown = (byte)stream.ReadByte(),
                        Offset = (int)stream.ReadUInt24(ByteOrder.BigEndian),
                        SpeakerID = (byte)stream.ReadByte(),
                        EventID = (byte)stream.ReadByte(),
                    },
                    _ => new(),
                }
            );
        }

        if(PaddingCount > 0)
        {
            long oldPos = stream.Position;
            long newPos = ByteConv.FindBytePattern(stream, [0xFF]);
            if (newPos != -1)
            {
                GapSize = (int)(newPos - oldPos);
                stream.Position -= 1;
            }
            stream.Position = oldPos;
            Unknown4 = stream.ReadBytes(GapSize);
            stream.Position = newPos;
        }
        Padding = stream.ReadBytes(PaddingCount);
    }

    public void Save(string path)
    {
        using var stream = File.OpenWrite(path);

        stream.WriteUInt16((ushort)Unknown1, ByteOrder.LittleEndian);
        stream.WriteUInt16((ushort)Unknown2, ByteOrder.LittleEndian);
        stream.WriteByte((byte)EntryTypes);
        stream.WriteByte((byte)FileHeaders.Count);
        stream.WriteByte((byte)Padding.Length);
        stream.WriteByte(AligmentSize);
        stream.WriteUInt16((ushort)Unknown3, ByteOrder.LittleEndian);

        stream.Position += EntryTypes switch
        {
            0 or 2 => 2,
            1 or 3 => 1,
            _ => 0,
        };

        for (int i = 0; i < FileHeaders.Count; i++)
        {
            var header = FileHeaders[i];
            switch (EntryTypes) {
            case 0:
                stream.WriteUInt16((ushort)header.Offset, ByteOrder.BigEndian);
                break;
            case 1 or 2:
                stream.WriteByte(header.Unknown);
                stream.WriteUInt16((ushort)header.Offset, ByteOrder.BigEndian);
                break;
            case 3:
                stream.WriteUInt24((uint)header.Offset, ByteOrder.BigEndian);
                stream.WriteByte(header.SpeakerID);
                stream.WriteByte(header.EventID);
                break;
            case 4:
                stream.WriteByte(header.Unknown);
                stream.WriteUInt24((uint)header.Offset, ByteOrder.BigEndian);
                stream.WriteByte(header.SpeakerID);
                stream.WriteByte(header.EventID);
                break;
            }
        }

        stream.Write(Unknown4);
        stream.Write(Padding);
    }

    public struct FileHeader
    {
        public byte Unknown;
        public byte SpeakerID;
        public byte EventID;
        public int Offset;
    }
}