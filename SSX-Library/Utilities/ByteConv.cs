using System.Collections;

namespace SSX_Library.Utilities;

/// <summary>
/// Converts bytes to other types, and does operations on them.
/// </summary>
public static class ByteConv
{
    public enum Nibble {High, Low}

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
    
} 