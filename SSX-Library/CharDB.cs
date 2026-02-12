using SSX_Library.Internal.Utilities;

namespace SSX_Library;


/// <summary>
/// Handler for the SSX3 CharDB.dbl file for storing character information.
/// </summary>
public class CharDB
{
    public enum InfoMode
    {
        Default,
        JP_Korean,
        JP_Korean_Chear_Characters,
    }

    private readonly List<DefaultInfo> DefaultInfoList = [];
    private readonly List<JPKoreanInfo> JPKoreanInfoList = [];
    private readonly List<JPKoreanCheatCharactersInfo> JPKoreanCheatCharactersInfoList = [];
    private InfoMode LoadedModeType = InfoMode.Default;

    public void Load(string loadPath, InfoMode infoMode = InfoMode.Default)
    {
        LoadedModeType = infoMode;
        DefaultInfoList.Clear();
        JPKoreanInfoList.Clear();
        JPKoreanCheatCharactersInfoList.Clear();

        using var stream = File.OpenRead(loadPath);
        while (stream.Position != stream.Length)
        {
            switch (infoMode)
            {
                case InfoMode.Default: 
                    ReadDefaultInfo(stream, DefaultInfoList); 
                    break;
                case InfoMode.JP_Korean: 
                    ReadJPKoreanInfo(stream, JPKoreanInfoList); 
                    break;
                case InfoMode.JP_Korean_Chear_Characters: 
                    ReadJPKoreanCheatCharactersInfo(stream, JPKoreanCheatCharactersInfoList); 
                    break;
            }
        }
    }

    public void Save(string savePath)
    {
        using var stream = File.Create(savePath);
        switch (LoadedModeType)
        {
            case InfoMode.Default: 
                WriteDefaultInfo(stream, DefaultInfoList); 
                break;
            case InfoMode.JP_Korean: 
                WriteJPKoreanInfo(stream, JPKoreanInfoList); 
                break;
            case InfoMode.JP_Korean_Chear_Characters: 
                WriteJPKoreanCheatCharactersInfo(stream, JPKoreanCheatCharactersInfoList); 
                break;
        }
    }

    private static void WriteDefaultInfo(Stream stream, List<DefaultInfo> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithLength(stream, info.LongName, 32);
            Writer.WriteASCIIStringWithLength(stream, info.FirstName, 16);
            Writer.WriteASCIIStringWithLength(stream, info.NickName, 16);
            Writer.WriteUInt32(stream, info.Weight, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Stance, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.ModelSize, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithLength(stream, info.BloodType, 16);
            Writer.WriteUInt32(stream, info.Gender, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Age, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithLength(stream, info.Height, 16);
            Writer.WriteASCIIStringWithLength(stream, info.Nationality, 16);
            Writer.WriteUInt32(stream, info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanInfo(Stream stream, List<JPKoreanInfo> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithLength(stream, info.FirstNameEnglish, 16);
            Writer.WriteASCIIStringWithLength(stream, info.LongName, 32);
            Writer.WriteASCIIStringWithLength(stream, info.FirstName, 16);
            Writer.WriteASCIIStringWithLength(stream, info.NickName, 16);
            Writer.WriteUInt32(stream, info.Weight, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Stance, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.ModelSize, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithLength(stream, info.BloodType, 16);
            Writer.WriteUInt32(stream, info.Gender, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, info.Age, ByteOrder.LittleEndian);
            Writer.WriteASCIIStringWithLength(stream, info.Height, 16);
            Writer.WriteASCIIStringWithLength(stream, info.Nationality, 16);
            Writer.WriteUInt32(stream, info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanCheatCharactersInfo(Stream stream, List<JPKoreanCheatCharactersInfo> infoList)
    {
        foreach (var info in infoList)
        {
            Writer.WriteASCIIStringWithLength(stream, info.FirstNameEnglish, 8);
            Writer.WriteASCIIStringWithLength(stream, info.LongName, 24);
        }  
    }

    private static void ReadDefaultInfo(Stream stream, List<DefaultInfo> infoList)
    {
        DefaultInfo info = new()
        {
            LongName = Reader.ReadASCIIStringWithLength(stream, 32),
            FirstName = Reader.ReadASCIIStringWithLength(stream, 16),
            NickName = Reader.ReadASCIIStringWithLength(stream, 16),
            Weight = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Stance = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            ModelSize = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            BloodType = Reader.ReadASCIIStringWithLength(stream, 16),
            Gender = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Age = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Height = Reader.ReadASCIIStringWithLength(stream, 16),
            Nationality = Reader.ReadASCIIStringWithLength(stream, 16),
            Position = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanInfo(Stream stream, List<JPKoreanInfo> infoList)
    {
        JPKoreanInfo info = new()
        {
            FirstNameEnglish = Reader.ReadASCIIStringWithLength(stream, 16),
            LongName = Reader.ReadASCIIStringWithLength(stream, 32),
            FirstName = Reader.ReadASCIIStringWithLength(stream, 16),
            NickName = Reader.ReadASCIIStringWithLength(stream, 16),
            Weight = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Stance = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            ModelSize = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            BloodType = Reader.ReadASCIIStringWithLength(stream, 16),
            Gender = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Age = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Height = Reader.ReadASCIIStringWithLength(stream, 16),
            Nationality = Reader.ReadASCIIStringWithLength(stream, 16),
            Position = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanCheatCharactersInfo(Stream stream, List<JPKoreanCheatCharactersInfo> infoList)
    {
        JPKoreanCheatCharactersInfo info = new()
        {
            FirstNameEnglish = Reader.ReadASCIIStringWithLength(stream, 8),
            LongName = Reader.ReadASCIIStringWithLength(stream, 24),
        };
        infoList.Add(info);
    }

    public struct DefaultInfo
    {
        public string LongName;
        public string FirstName;
        public string NickName;
        public uint Weight;
        public uint Stance;
        public uint ModelSize;
        public string BloodType;
        public uint Gender;
        public uint Age;
        public string Height;
        public string Nationality;
        public uint Position;
    }

    public struct JPKoreanInfo
    {
        public string FirstNameEnglish;
        public string LongName;
        public string FirstName;
        public string NickName;
        public uint Weight;
        public uint Stance;
        public uint ModelSize;
        public string BloodType;
        public uint Gender;
        public uint Age;
        public string Height;
        public string Nationality;
        public uint Position;
    }

    public struct JPKoreanCheatCharactersInfo
    {
        public string FirstNameEnglish;
        public string LongName;
    }
}


