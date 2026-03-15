using SSX_Library.Internal.Utilities.StreamExtensions;

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
            stream.WriteAsciiWithLength(info.LongName, 32);
            stream.WriteAsciiWithLength(info.FirstName, 16);
            stream.WriteAsciiWithLength(info.NickName, 16);
            stream.WriteUInt32(info.Weight, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.Stance, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.ModelSize, ByteOrder.LittleEndian);
            stream.WriteAsciiWithLength(info.BloodType, 16);
            stream.WriteUInt32(info.Gender, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.Age, ByteOrder.LittleEndian);
            stream.WriteAsciiWithLength(info.Height, 16);
            stream.WriteAsciiWithLength(info.Nationality, 16);
            stream.WriteUInt32(info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanInfo(Stream stream, List<Info> infoList)
    {
        foreach (var info in infoList)
        {
            stream.WriteAsciiWithLength(info.FirstNameEnglish, 16);
            stream.WriteUtf16WithLength(info.LongName, 32);
            stream.WriteUtf16WithLength(info.FirstName, 16);
            stream.WriteUtf16WithLength(info.NickName, 16);
            stream.WriteUInt32(info.Weight, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.Stance, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.ModelSize, ByteOrder.LittleEndian);
            stream.WriteAsciiWithLength(info.BloodType, 16);
            stream.WriteUInt32(info.Gender, ByteOrder.LittleEndian);
            stream.WriteUInt32(info.Age, ByteOrder.LittleEndian);
            stream.WriteAsciiWithLength(info.Height, 16);
            stream.WriteAsciiWithLength(info.Nationality, 16);
            stream.WriteUInt32(info.Position, ByteOrder.LittleEndian);
        }
    }

    private static void WriteJPKoreanCheatCharactersInfo(Stream stream, List<Info> infoList)
    {
        foreach (var info in infoList)
        {
            stream.WriteAsciiWithLength(info.FirstNameEnglish, 8);
            stream.WriteUtf16WithLength(info.LongName, 24);
        }
    }

    private static void ReadDefaultInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            LongName = stream.ReadAsciiWithLength(32, false),
            FirstName = stream.ReadAsciiWithLength(16, false),
            NickName = stream.ReadAsciiWithLength(16, false),
            Weight = stream.ReadUInt32(ByteOrder.LittleEndian),
            Stance = stream.ReadUInt32(ByteOrder.LittleEndian),
            ModelSize = stream.ReadUInt32(ByteOrder.LittleEndian),
            BloodType = stream.ReadAsciiWithLength(16, false),
            Gender = stream.ReadUInt32(ByteOrder.LittleEndian),
            Age = stream.ReadUInt32(ByteOrder.LittleEndian),
            Height = stream.ReadAsciiWithLength(16, false),
            Nationality = stream.ReadAsciiWithLength(16, false),
            Position = stream.ReadUInt32(ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            FirstNameEnglish = stream.ReadAsciiWithLength(16, false),
            LongName = stream.ReadUtf16WithByteLength(32, false),
            FirstName = stream.ReadUtf16WithByteLength(16, false),
            NickName = stream.ReadUtf16WithByteLength(16, false),
            Weight = stream.ReadUInt32(ByteOrder.LittleEndian),
            Stance = stream.ReadUInt32(ByteOrder.LittleEndian),
            ModelSize = stream.ReadUInt32(ByteOrder.LittleEndian),
            BloodType = stream.ReadAsciiWithLength(16, false),
            Gender = stream.ReadUInt32(ByteOrder.LittleEndian),
            Age = stream.ReadUInt32(ByteOrder.LittleEndian),
            Height = stream.ReadAsciiWithLength(16, false),
            Nationality = stream.ReadAsciiWithLength(16, false),
            Position = stream.ReadUInt32(ByteOrder.LittleEndian),
        };
        infoList.Add(info);
    }

    private static void ReadJPKoreanCheatCharactersInfo(Stream stream, List<Info> infoList)
    {
        Info info = new()
        {
            FirstNameEnglish = stream.ReadAsciiWithLength(8, false),
            LongName = stream.ReadUtf16WithByteLength(24, false),
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
        public uint ModelSize;
        public string BloodType;
        public uint Gender;
        public uint Age;
        public string Height;
        public string Nationality;
        public uint Position;
    }
}


