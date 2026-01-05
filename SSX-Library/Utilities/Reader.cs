using System.Buffers.Binary;
using System.Text;

namespace SSX_Library.Utilities;

/// <summary>
/// Reads primitive types from a stream
/// </summary>
internal static class Reader
{
    private static ByteOrder _defaultMode = ByteOrder.LittleEndian;
    public static ByteOrder DefaultMode { get { return _defaultMode; } }

    /// <summary>
    /// Define a Default Reading Mode to be used.
    /// Best used if the file doesn't switch between Little and Big Endian
    /// </summary>
    /// <param name="byteOrder"></param>
    public static void SetDefaultMode(ByteOrder byteOrder)
    {
        _defaultMode = byteOrder;

        if (ByteOrder.Default == byteOrder)
        {
            byteOrder = ByteOrder.LittleEndian;
        }
    }

    public static byte[] ReadBytes(Stream stream, int length)
    {
        var buf = new byte[length];
        stream.Read(buf);
        return buf;
    }

    public static ushort ReadUInt16(Stream stream, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[2];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt16BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt16LittleEndian(buf),
            _ => 0
        };
    }

    public static uint ReadUInt24(Stream stream, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[3];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => (uint)(buf[0] << 16 | buf[1] << 8 | buf[2]),
            ByteOrder.LittleEndian => (uint)(buf[2] << 16 | buf[1] << 8 | buf[0]),
            _ => 0
        };
    }

    public static uint ReadUInt32(Stream stream, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[4];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt32BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt32LittleEndian(buf),
            _ => 0
        };
    }
    public static ulong ReadUInt64(Stream stream, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[8];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt64BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt64LittleEndian(buf),
            _ => 0
        };
    }

    public static float ReadFloat(Stream stream, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[4];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadSingleBigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadSingleLittleEndian(buf),
            _ => 0
        };
    }
    public static string ReadNullTerminatedASCIIString(Stream stream)
    {
        List<byte> text = [];
        while (true)
        {
            int letter = stream.ReadByte();
            if (letter <= 0) break;
            text.Add((byte)letter);
        }
        return Encoding.ASCII.GetString([.. text]);
    }

    public static string ReadASCIIStringWithLength(Stream stream, int length, bool removeNullChars = true)
    {
        var buf = new byte[length];
        stream.Read(buf);
        if (removeNullChars)
        {
            return Encoding.ASCII.GetString([.. buf.Where(x => x != '\0')]);
        }
        return Encoding.ASCII.GetString(buf);
    }

}