using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace SSX_Library.Internal.Utilities.StreamExtensions;

/// <summary>
/// Stream extensions for reading primitive types.
/// </summary>
internal static class Reader
{
    static ByteOrder DefaultReadMode = ByteOrder.LittleEndian;
    public static void SetDefaultReadMode(ByteOrder byteOrder)
    {
        DefaultReadMode = byteOrder;
    }

    public static ByteOrder ReturnDefaultReadMode()
    {
        return DefaultReadMode;
    }

    public static byte[] ReadBytes(this Stream stream, int length)
    {
        var buf = new byte[length];
        stream.Read(buf);
        return buf;
    }

    public static ushort ReadUInt16(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[2];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt16BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt16LittleEndian(buf),
            _ => 0
        };
    }

    public static uint ReadUInt24(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[3];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => (uint)(buf[0] << 16 | buf[1] << 8 | buf[2]),
            ByteOrder.LittleEndian => (uint)(buf[2] << 16 | buf[1] << 8 | buf[0]),
            _ => 0
        };
    }

    public static uint ReadUInt32(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[4];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt32BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt32LittleEndian(buf),
            _ => 0
        };
    }

    public static int ReadInt32(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[4];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadInt32BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadInt32LittleEndian(buf),
            _ => 0
        };
    }

    public static ulong ReadUInt64(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[8];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt64BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt64LittleEndian(buf),
            _ => 0
        };
    }

    public static float ReadFloat(this Stream stream, ByteOrder? byteOrder = null)
    {
        var buf = new byte[4];
        stream.Read(buf);

        if (!byteOrder.HasValue)
        {
            byteOrder = DefaultReadMode;
        }

        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadSingleBigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadSingleLittleEndian(buf),
            _ => 0
        };
    }

    public static Vector3 ReadVector3(this Stream stream, ByteOrder? byteOrder = null)
    {
        return new Vector3(ReadFloat(stream, byteOrder), ReadFloat(stream, byteOrder), ReadFloat(stream, byteOrder));
    }

    public static Vector4 ReadVector4(this Stream stream, ByteOrder? byteOrder = null)
    {
        return new Vector4(ReadFloat(stream, byteOrder), ReadFloat(stream, byteOrder), ReadFloat(stream, byteOrder), ReadFloat(stream, byteOrder));
    }

    public static string ReadAsciiNullTerminated(this Stream stream)
    {
        List<byte> text = [];
        while (true)
        {
            int letter = stream.ReadByte();
            if (letter == 0) break;
            text.Add((byte)letter);
        }
        return Encoding.ASCII.GetString([..text]);
    }

    /// <param name="removeNullChars"> Return the string with null characters removed</param>
    public static string ReadAsciiWithLength(this Stream stream, int length, bool removeNullChars)
    {
        var buf = new byte[length];
        stream.Read(buf);
        if (removeNullChars)
        {
            return Encoding.ASCII.GetString([..buf.Where(x => x != '\0')]);
        }
        return Encoding.ASCII.GetString(buf);
    }

    /// <param name="removeNullChars"> Remove null characters if within the string</param>
    public static string ReadUtf16WithByteLength(this Stream stream, int byteLength, bool removeNullChars)
    {
        var buf = new byte[byteLength];
        stream.Read(buf);
        if (removeNullChars)
        {
            return Encoding.Unicode.GetString(buf).Replace("\0", "");
        }
        return Encoding.Unicode.GetString(buf);
    }
}