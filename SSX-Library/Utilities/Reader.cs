using System.Buffers.Binary;

namespace SSX_Library.Utilities;

/// <summary>
/// Reads primitive types from a stream
/// </summary>
internal static class Reader
{
    public static byte[] ReadBytes(Stream stream, int length)
    {
        var buf = new byte[length];
        stream.Read(buf);
        return buf;
    }

    public static uint ReadUInt32(Stream stream, ByteOrder byteOrder)
    {
        var buf = new byte[4];
        stream.Read(buf);
        return byteOrder switch
        {
            ByteOrder.BigEndian => BinaryPrimitives.ReadUInt32BigEndian(buf),
            ByteOrder.LittleEndian => BinaryPrimitives.ReadUInt32LittleEndian(buf),
            _ => 0
        };
    }

    public static float ReadFloat(Stream stream, ByteOrder byteOrder)
    {
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
        return System.Text.Encoding.ASCII.GetString([..text]);
    }



}