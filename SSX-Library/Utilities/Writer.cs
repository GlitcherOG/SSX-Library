using System.Buffers.Binary;

namespace SSX_Library.Utilities;

/// <summary>
/// Writes primitive types to a stream
/// </summary>
internal static class Writer
{
    private static ByteOrder _defaultMode = ByteOrder.LittleEndian;
    public static ByteOrder DefaultMode { get { return _defaultMode; } }

    /// <summary>
    /// Define a Default Writing Mode to be used.
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

    public static void WriteBytes(Stream stream, byte[] buffer)
    {
        stream.Write(buffer);
    }
    public static void WriteUInt16(Stream stream, ushort value, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[2];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        }
        else if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteUInt24(Stream stream, uint value, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[3];
        if (byteOrder == ByteOrder.BigEndian)
        {
            buf[0] = (byte)(value >> 16 & 0xFF);
            buf[1] = (byte)(value >> 8 & 0xFF);
            buf[2] = (byte)(value & 0xFF);
        }
        else if (byteOrder == ByteOrder.LittleEndian)
        {
            buf[0] = (byte)(value & 0xFF);
            buf[1] = (byte)(value >> 8 & 0xFF);
            buf[2] = (byte)(value >> 16 & 0xFF);
        }
        stream.Write(buf);
    }

    public static void WriteUInt32(Stream stream, uint value, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

        var buf = new byte[4];
        if (byteOrder == ByteOrder.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        }
        else if (byteOrder == ByteOrder.LittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        }
        stream.Write(buf);
    }

    public static void WriteFloat(Stream stream, float value, ByteOrder byteOrder = ByteOrder.Default)
    {
        if (byteOrder == ByteOrder.Default)
        {
            byteOrder = DefaultMode;
        }

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
}