using SSX_Library.Internal.Utilities;

namespace SSX_Library;

/// <summary>
/// Handler for the SSX3 CharDB.dbl file for storing character information.
/// </summary>
/// <remarks>
/// Be careful when changing info strings. Make sure to stay within the
/// string length bounds of the specific info mode.
/// </remarks>
public class CharDB
{
    public enum InfoMode
    {
        Default,
        JP_Korean,
        JP_Korean_Chear_Characters,
    }

    /// <summary>
    /// The list of character information.
    /// </summary>
    public List<Info> InfoList = [];

    /// <summary>
    /// Info type/version depending on your game.
    /// </summary>
    public InfoMode LoadedModeType { get{ return _LoadedModeType; } }
    private InfoMode _LoadedModeType = InfoMode.Default;

    /// <summary>
    /// Load the SSX3 CharDB.dbl file to memory (this class)
    /// </summary>
    /// <param name="infoMode"> The game version this file belongs to.</param>
    public void Load(string path, InfoMode infoMode)
    {
        _LoadedModeType = infoMode;
        InfoList.Clear();

        using var stream = File.OpenRead(path);
        while (stream.Position != stream.Length)
        {
            switch (infoMode)
            {
                case InfoMode.Default: 
                    ReadDefaultInfo(stream, InfoList); 
                    break;
                case InfoMode.JP_Korean: 
                    ReadJPKoreanInfo(stream, InfoList); 
                    break;
                case InfoMode.JP_Korean_Chear_Characters: 
                    ReadJPKoreanCheatCharactersInfo(stream, InfoList); 
                    break;
            }
        }
    }

    /// <summary>
    /// Load the SSX3 CharDB.dbl file to disc
    /// </summary>
    public void Save(string path)
    {
        using var stream = File.Create(path);
        switch (_LoadedModeType)
        {
            case InfoMode.Default: 
                WriteDefaultInfo(stream, InfoList); 
                break;
            case InfoMode.JP_Korean: 
                WriteJPKoreanInfo(stream, InfoList); 
                break;
            case InfoMode.JP_Korean_Chear_Characters: 
                WriteJPKoreanCheatCharactersInfo(stream, InfoList); 
                break;
        }
    }

    private static void WriteDefaultInfo(Stream stream, List<Info> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithNullLength(stream, info.LongName, 32);
            Writer.WriteASCIIStringWithNullLength(stream, info.FirstName, 16);
            Writer.WriteASCIIStringWithNullLength(stream, info.NickName, 16);
            Writer.WriteUInt32(stream, info.Weight, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Stance, ByteOrder.LittleEndian);
            Writer.WriteInt32(stream, info.ModelSize, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithNullLength(stream, info.BloodType, 16);
            Writer.WriteUInt32(stream, info.Gender, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Age, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithNullLength(stream, info.Height, 16);
            Writer.WriteASCIIStringWithNullLength(stream, info.Nationality, 16);
            Writer.WriteUInt32(stream, info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanInfo(Stream stream, List<Info> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithNullLength(stream, info.FirstNameEnglish, 16);
            Writer.WriteStringUTF16WithNullLength(stream, info.LongName, 32);
            Writer.WriteStringUTF16WithNullLength(stream, info.FirstName, 16);
            Writer.WriteStringUTF16WithNullLength(stream, info.NickName, 16);
            Writer.WriteUInt32(stream, info.Weight, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Stance, ByteOrder.LittleEndian);
            Writer.WriteInt32(stream, info.ModelSize, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithNullLength(stream, info.BloodType, 16);
            Writer.WriteUInt32(stream, info.Gender, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Age, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithNullLength(stream, info.Height, 16);
            Writer.WriteASCIIStringWithNullLength(stream, info.Nationality, 16);
            Writer.WriteUInt32(stream, info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanCheatCharactersInfo(Stream stream, List<Info> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithNullLength(stream, info.FirstNameEnglish, 8);
            Writer.WriteStringUTF16WithNullLength(stream, info.LongName, 24);
        }  
    }

    private static void ReadDefaultInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            LongName = Reader.ReadASCIIStringWithLength(stream, 32),
            FirstName = Reader.ReadASCIIStringWithLength(stream, 16),
            NickName = Reader.ReadASCIIStringWithLength(stream, 16),
            Weight = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Stance = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            ModelSize = Reader.ReadInt32(stream, ByteOrder.LittleEndian),
            BloodType = Reader.ReadASCIIStringWithLength(stream, 16),
            Gender = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Age = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Height = Reader.ReadASCIIStringWithLength(stream, 16),
            Nationality = Reader.ReadASCIIStringWithLength(stream, 16),
            Position = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            FirstNameEnglish = Reader.ReadASCIIStringWithLength(stream, 16),
            LongName = Reader.ReadStringUTF16(stream, 32),
            FirstName = Reader.ReadStringUTF16(stream, 16),
            NickName = Reader.ReadStringUTF16(stream, 16),
            Weight = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Stance = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            ModelSize = Reader.ReadInt32(stream, ByteOrder.LittleEndian),
            BloodType = Reader.ReadASCIIStringWithLength(stream, 16),
            Gender = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Age = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Height = Reader.ReadASCIIStringWithLength(stream, 16),
            Nationality = Reader.ReadASCIIStringWithLength(stream, 16),
            Position = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanCheatCharactersInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            FirstNameEnglish = Reader.ReadASCIIStringWithLength(stream, 8),
            LongName = Reader.ReadStringUTF16(stream, 24),
        };
        infoList.Add(info);
    }

    public struct Info
    {
        public string FirstNameEnglish;
        public string LongName;
        public string FirstName;
        public string NickName;
        public uint Weight;
        public uint Stance;
        public int ModelSize;
        public string BloodType;
        public uint Gender;
        public uint Age;
        public string Height;
        public string Nationality;
        public uint Position;
    }
}


