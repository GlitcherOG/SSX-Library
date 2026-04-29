
using SSX_Library.Internal.Utilities.StreamExtensions;
using SSX_Library.Internal.Utilities;

namespace SSX_Library.Internal.Audio;

internal partial class DAT
{
    private class HDR
    {
        public short Unknown1; // U1
        public short Unknown2; // U2
        public ushort EntryTypes;
        public byte FileCount;
        public byte PaddingCount;
        public byte AligmentSize;
        public short Unknown3; // U5
        public int GapSize;
        public List<FileHeader> fileHeaders = [];
        public List<byte> Padding = [];

        public void Load(string path)
        {
            using var stream = File.OpenRead(path);
            Unknown1 = stream.ReadInt16(ByteOrder.LittleEndian);
            Unknown2 = stream.ReadInt16(ByteOrder.LittleEndian); //Always -1
            EntryTypes = stream.ReadUInt16(ByteOrder.LittleEndian);
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

            fileHeaders = [];
            for (int _ = 0; _ < FileCount; _++)
            {
                fileHeaders.Add(
                    EntryTypes switch
                    {
                        0 => new()
                        {
                            OffsetInt = stream.ReadInt16(ByteOrder.BigEndian),
                        },
                        1 => new()
                        {
                            Unknown1 = (byte)stream.ReadByte(),
                            OffsetInt = stream.ReadInt16(ByteOrder.BigEndian),
                        },
                        2 => new()
                        {
                            OffsetInt = stream.ReadInt16(ByteOrder.BigEndian),
                            Unknown2 = (byte)stream.ReadByte(),
                            EventID = (byte)stream.ReadByte(),
                        },
                        3 => new()
                        {
                            OffsetInt = (int)stream.ReadUInt24(ByteOrder.BigEndian),
                            Unknown2 = (byte)stream.ReadByte(),
                            EventID = (byte)stream.ReadByte(),
                        },
                        4 => new()
                        {
                            Unknown1 = (byte)stream.ReadByte(),
                            OffsetInt = (int)stream.ReadUInt24(ByteOrder.BigEndian),
                            Unknown2 = (byte)stream.ReadByte(),
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
            }

            Padding = [];
            for (int _ = 0; _ < PaddingCount; _++)
            {
                Padding.Add((byte)stream.ReadByte());
            }
        }



        public struct FileHeader
        {
            public byte Unknown1;
            public byte[] Offset;
            public byte Unknown2;
            public byte EventID;
            public int OffsetInt;
        }
    }
}