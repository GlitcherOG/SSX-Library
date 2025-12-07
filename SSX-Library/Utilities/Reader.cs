using System.Buffers.Binary;

namespace SSX_Library.Utilities;

/// <summary>
/// Reads types from a stream
/// </summary>
internal static class Reader
{
    public static byte[] ReadBytes(Stream stream, int length)
    {
        var buf = new byte[length];
        stream.Read(buf);
        return buf;
    }

    public static uint ReadUint32(Stream stream, ByteOrder byteOrder)
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


    


}