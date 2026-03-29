using System.Buffers.Binary;
using System.Text;

namespace SSX_Library.Internal.Utilities.StreamExtensions;

/// <summary>
/// Stream extensions for writing primitive types.
/// </summary>
internal static class Writer
{
    public static void WriteUInt16(this Stream stream, ushort value, ByteOrder byteOrder)
    {
        var buf = new byte[2];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        }
        else if(byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteUInt24(this Stream stream, uint value, ByteOrder byteOrder)
    {
        var buf = new byte[3];
        if (byteOrder == ByteOrder.BigEndian)
        {
            buf[0] = (byte)(value >> 16 & 0xFF);
            buf[1] = (byte)(value >> 8 & 0xFF);
            buf[2] = (byte)(value & 0xFF);
        }
        else if(byteOrder == ByteOrder.LittleEndian)
        {
            buf[0] = (byte)(value & 0xFF);
            buf[1] = (byte)(value >> 8 & 0xFF);
            buf[2] = (byte)(value >> 16 & 0xFF);
        }
        stream.Write(buf);
    }

    public static void WriteUInt32(this Stream stream, uint value, ByteOrder byteOrder)
    {
        var buf = new byte[4];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        }
        else if(byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteInt32(this Stream stream, int value, ByteOrder byteOrder)
    {
        var buf = new byte[4];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteInt32BigEndian(buf, value);
        }
        else if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        }
        stream.Write(buf);
    }
    
    public static void WriteUInt64(this Stream stream, ulong value, ByteOrder byteOrder)
    {
        var buf = new byte[8];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buf, value);
        }
        else if(byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteFloat(this Stream stream, float value, ByteOrder byteOrder)
    {
        var buf = new byte[4];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteSingleBigEndian(buf, value);
        }
        else if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteSingleLittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteAsciiNullTerminated(this Stream stream, string text)
    {
        stream.Write(Encoding.ASCII.GetBytes(text));
        stream.Write([0]); // Null
    }

    /// <summary>
    /// Write an ascii string with a set length of characters. Surpassing the text's length
    /// will result in appended null characters.
    /// </summary>
    public static void WriteAsciiWithLength(this Stream stream, string text, int length)
    {
        byte[] textBytes = Encoding.ASCII.GetBytes(text);
        byte[] buf = new byte[length];
        Buffer.BlockCopy(textBytes, 0, buf, 0, Math.Min(length, textBytes.Length));
        stream.Write(buf);
    }

    public static void WriteUtf16(this Stream stream, string text)
    {
        stream.Write(Encoding.Unicode.GetBytes(text));
    }

    /// <summary>
    /// Write a utf16 string with a set length of characters. Surpassing the text's length
    /// will result in appended utf16 null characters.
    /// </summary>
    /// <remarks>
    /// Length is per utf16 character (2 bytes).
    /// </remarks>
    public static void WriteUtf16WithLength(this Stream stream, string text, int length)
    {
        byte[] textBytes = Encoding.Unicode.GetBytes(text);
        byte[] buf = new byte[length * 2];
        Buffer.BlockCopy(textBytes, 0, buf, 0, Math.Min(textBytes.Length, buf.Length));
        stream.Write(buf);
    }
}