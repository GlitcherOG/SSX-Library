using System.Buffers.Binary;
using ICSharpCode.SharpZipLib;

namespace SSX_Library.Internal.Utilities;

/// <summary>
/// Writes primitive types to a stream
/// </summary>
internal static class Writer
{
    public static void WriteByte(Stream stream, byte value)
    {
        stream.WriteByte(value);
    }

    public static void WriteBytes(Stream stream, byte[] buffer)
    {
        stream.Write(buffer);
    }

    public static void WriteBytes(Stream stream, byte[] buffer, int offset, int count)
    {
        stream.Write(buffer, offset, count);
    }

    public static void WriteUInt16(Stream stream, ushort value, ByteOrder byteOrder)
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

    public static void WriteUInt24(Stream stream, uint value, ByteOrder byteOrder)
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

    public static void WriteUInt32(Stream stream, uint value, ByteOrder byteOrder)
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

    public static void WriteUInt64(Stream stream, ulong value, ByteOrder byteOrder)
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

    public static void WriteFloat(Stream stream, float value, ByteOrder byteOrder)
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

    /// <remarks>
    /// Includes null char
    /// </remarks>
    public static void WriteNullTerminatedASCIIString(Stream stream, string text)
    {
        stream.Write(System.Text.Encoding.ASCII.GetBytes(text));
        stream.Write([0]); // Null
    }

    /// <remarks>
    /// Excludes null char
    /// </remarks>
    public static void WriteASCIIStringWithLength(Stream stream, string text, int length)
    {
        if (length > text.Length)
        {
            throw new ValueOutOfRangeException("Length out of range");
        }
        stream.Write(System.Text.Encoding.ASCII.GetBytes(text));
    }

    /// <summary>
    /// Fill empty characters with null
    /// </summary>
    /// <exception cref="ValueOutOfRangeException"></exception>
    public static void WriteASCIIStringWithNullLength(Stream stream, string text, int length)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i < length; i++)
        {
            if (i < bytes.Length)
            {
                stream.WriteByte(bytes[i]);
            }
            else
            {
                stream.WriteByte(0);
            }
        }
    }

    public static void WriteStringUTF16(Stream stream, string text, int byteLength = 0)
    {
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text);
        if (byteLength == 0)
        {
            stream.Write(bytes);
            return;
        }
        int bytesToWrite = Math.Min(bytes.Length, byteLength);
        stream.Write(bytes, 0, bytesToWrite);
    }

    /// <summary>
    /// Write the UTF16 string to a stream with a length, Write null characters if
    /// the string does dont fill the whole length.
    /// </summary>
    public static void WriteStringUTF16WithNullLength(Stream stream, string text, int byteLength)
    {
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text);
        for (int i = 0; i < byteLength; i++)
        {
            if (i < bytes.Length)
            {
                stream.WriteByte(bytes[i]);
            }
            else
            {
                stream.WriteByte(0);
            }
        }
    }
}