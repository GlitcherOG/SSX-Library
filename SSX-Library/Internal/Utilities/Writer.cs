using System.Buffers.Binary;

namespace SSX_Library.Internal.Utilities;

/// <summary>
/// Writes primitive types to a stream
/// </summary>
internal static class Writer
{
    public static void WriteBytes(Stream stream, byte[] buffer)
    {
        stream.Write(buffer);
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
}