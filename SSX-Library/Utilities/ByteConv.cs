using System.Diagnostics;

namespace SSX_Library.Utilities;

/// <summary>
/// Converts bytes to other types, and does operations on them.
/// </summary>
public static class ByteConv
{
    public enum Nibble {High, Low};
    public enum ByteOrder {BigEndian, LittleEndian};

    private const int LowNibbleMask = 0xF; // 0b0000_1111
    private const int HighNibbleMask = 0xF0; // 0b1111_0000
    private const int Int9Mask = 0x1FF; //0b1_1111_1111
    private const int Int12Mask = 0xFFF; //0b1111_1111_1111

    /// <summary>
    /// Get the nibble of a byte
    /// </summary>
    /// <returns>The nibble as a byte</returns>
    public static byte GetByteNibble(byte aByte, Nibble nibble)
    {
        return nibble switch
        {
            Nibble.High => (byte)((aByte & HighNibbleMask) >> 4),
            Nibble.Low => (byte)(aByte & LowNibbleMask),
            _ => 0
        };
    }

    /// <summary>
    /// Sets the nibble of a byte.
    /// </summary>
    /// <param name="nibbleByte">The nibble value to use (Only uses the first 4 bits)</param>
    /// <returns>The byte with the new nibble</returns>
    public static byte SetByteNibble(byte srcByte, byte nibbleByte, Nibble nibble)
    {
        return nibble switch
        {
            Nibble.High => (byte)((srcByte & LowNibbleMask) | ((nibbleByte & LowNibbleMask) << 4)),
            Nibble.Low =>  (byte)((srcByte & HighNibbleMask) | (nibbleByte & LowNibbleMask)),
            _ => 0
        };
    }

    /// <summary>
    /// Converts 4 bytes to an array of 9bit integers. 
    /// </summary>
    /// <param name="byteOrder"> The endianess of the input Bytes array </param>
    /// <returns>An array of three 9bit integers,
    /// returned from least to most significant. </returns>
    public static int[] BytesToInt9Array(byte[] Bytes, ByteOrder byteOrder)
    {
        Debug.Assert(Bytes.Length >= 4, "Not enough bytes passed");
        byte[] array = [..Bytes];
        if (byteOrder == ByteOrder.LittleEndian) Array.Reverse(array);
        int integer = BitConverter.ToInt32(array);

        int[] output = new int[3];
        for (int i = 0; i < 3; i++)
        {
            output[i] = integer & Int9Mask;
            integer >>= 9;
        }
        return output;
    }
    
    /// <summary>
    /// Converts 2 bytes to an int12. 
    /// <param name="byteOrder"> The endianess of the input Bytes array </param>
    /// </summary>
    public static int BytesToInt12(byte[] Bytes, ByteOrder byteOrder)
    {
        Debug.Assert(Bytes.Length >= 2, "Not enough bytes passed");
        byte[] array = [..Bytes];
        if (byteOrder == ByteOrder.LittleEndian) Array.Reverse(array);
        short integer = BitConverter.ToInt16(array);
        return integer & Int12Mask;
    }

    /// <summary>
    /// Searches a stream for a byte pattern.
    /// </summary>
    /// <param name="searchLimit"> The max amount of bytes to check before 
    /// stopping. -1 if you want to search the whole stream. </param>
    /// <returns>The offset from the start of the stream to the
    /// first byte of the pattern.</returns>
    public static long FindBytePattern(Stream stream, byte[] pattern, long searchLimit = -1)
    {
        Debug.Assert(pattern.Length >= 1, "Not enough bytes passed");
        Debug.Assert(searchLimit >= -1, "maxSearchLength cannot be less than -1");
        long endPosition = searchLimit switch
        {
            -1 => stream.Length,
            _ => searchLimit
        };

        long index = 0;
        while (true)
        {
            int readByte = stream.ReadByte();
            if (readByte == -1) break;
            if (stream.Position >= endPosition) break;

            if (readByte == pattern[index])
            {
                index++;
                if (index == pattern.Length)
                {
                    return stream.Position - pattern.Length;
                }
            }
            else
            {
                index = readByte == pattern[0] ? 1 : 0;
            }
        }
        return -1;
    }
} 