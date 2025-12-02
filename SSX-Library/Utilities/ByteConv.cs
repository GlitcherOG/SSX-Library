using System.Diagnostics;

namespace SSX_Library.Utilities;

/// <summary>
/// Converts bytes to other types, and does operations on them.
/// </summary>
public static class ByteConv
{
    public enum Nibble {High, Low};
    public enum ByteOrder {BigEndian, LittleEndian};

    /// <summary>
    /// Get the nibble of a byte
    /// </summary>
    /// <returns>The nibble as a byte</returns>
    public static byte GetByteNibble(byte aByte, Nibble nibble)
    {
        return nibble switch
        {
            Nibble.High => (byte)((aByte & 0b1111_0000) >> 4),
            Nibble.Low => (byte)(aByte & 0b1111),
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
            output[i] = integer & 0b1_1111_1111;
            integer >>= 9;
        }
        return output;
    }
    
} 