using System.Buffers.Binary;

namespace SSX_Library.Utilities;

/// <summary>
/// Writes primitive types to a stream
/// </summary>
internal static class Writer
{
    public static void WriteBytes(Stream stream, byte[] buffer)
    {
        stream.Write(buffer);
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
}