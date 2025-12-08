using System.Diagnostics;

namespace SSX_Library.Utilities;

/// <summary>
/// Converts bytes to other types, and does operations on them.
/// </summary>
internal static class ByteConv
{
    public enum Nibble {High, Low};

    private const int LowNibbleMask = 0xF;   // 0b0000_1111
    private const int HighNibbleMask = 0xF0; // 0b1111_0000
    private const int Int9Mask = 0x1FF;      // 0b1_1111_1111
    private const int Int12Mask = 0xFFF;     // 0b1111_1111_1111

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
    /// Searches for a byte pattern inside a stream.
    /// </summary>
    /// <param name="searchLimit"> The max amount of bytes to check before 
    /// stopping. -1 if you want to search the whole stream. </param>
    /// <returns> The distance from the start of the stream to the first byte of the
    /// first occurence of the pattern. Returns -1 if not found.</returns>
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
            if (stream.Position > endPosition) break;

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

    /// <summary>
    /// Swaps two bits from a byte.
    /// </summary>
    /// <returns>The byte with the bits swapped.</returns>
    public static byte ByteBitSwap(byte Byte, int BitA = 3, int BitB = 4)
    {
        Debug.Assert(BitA >= 0 && BitA <= 7 && BitB >= 0 && BitB <= 7, "Bits out of range.");
        Debug.Assert(BitA != BitB, "Bits cannot be the same.");
        int bitAValue = ((1 << BitA) & Byte) >> BitA;
        int bitBValue = ((1 << BitB) & Byte) >> BitB;
 
        // Clear bits
        Byte &= (byte)~(1 << BitA);
        Byte &= (byte)~(1 << BitB);

        // Set bits
        Byte |= (byte)(bitAValue << BitB);
        Byte |= (byte)(bitBValue << BitA);
        return Byte;
    }
    
    // Todo: Document
    // Morton ordering??? Z-Ordering???
    public static byte[] Swizzle8(byte[] buf, int width, int height)
    {
        byte[] output = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Swizzle mapping math (same as in Unswizzle)
                int blockLocation = (y & ~0xf) * width + (x & ~0xf) * 2;
                int swapSelector = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & ~3) >> 1) + (y & 1)) & 0x7;
                int columnLocation = posY * width * 2 + ((x + swapSelector) & 0x7) * 4;
                int byteNum = ((y >> 1) & 1) + ((x >> 2) & 2);
                int swizzleId = blockLocation + columnLocation + byteNum;

                // Now swizzle: copy from linear buf into swizzled output
                if (swizzleId < output.Length && y * width + x < buf.Length)
                {
                    output[swizzleId] = buf[y * width + x];
                }
            }
        }

        return output;
    }
    
    // Todo: Document
    public static byte[] Unswizzle8(byte[] buf, int width, int height)
    {
        byte[] output = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int blockLocation = (y & ~0xf) * width + (x & ~0xf) * 2;
                int swapSelector = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & ~3) >> 1) + (y & 1)) & 0x7;
                int columnLocation = posY * width * 2 + ((x + swapSelector) & 0x7) * 4;
                int byteNum = ((y >> 1) & 1) + ((x >> 2) & 2);
                int swizzleId = blockLocation + columnLocation + byteNum;

                if (swizzleId < buf.Length && y * width + x < output.Length)
                {
                    output[y * width + x] = buf[swizzleId];
                }
            }
        }

        return output;
    }
    
    // Todo: Document
    public static byte[] Swizzle4bpp(byte[] linearTexels, int width, int height)
    {
        byte[] swizzledTexels = new byte[width * height / 2];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                int byteIndex = index >> 1;

                // Extract 4-bit pixel from linear buffer
                int uPen;
                byte pix = linearTexels[byteIndex];

                if ((index & 1) != 0)
                    uPen = (pix >> 4) & 0xF;
                else
                    uPen = pix & 0xF;

                // Swizzle address calculation (same math as Unswizzle)
                int pageX = x & (~0x7f);
                int pageY = y & (~0x7f);

                int pages_horz = (width + 127) / 128;
                int pages_vert = (height + 127) / 128;

                int page_number = (pageY / 128) * pages_horz + (pageX / 128);

                int page32Y = (page_number / pages_vert) * 32;
                int page32X = (page_number % pages_vert) * 64;

                int page_location = page32Y * height * 2 + page32X * 4;

                int locX = x & 0x7f;
                int locY = y & 0x7f;

                int block_location = ((locX & (~0x1f)) >> 1) * height + (locY & (~0xf)) * 2;
                int swap_selector = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & (~3)) >> 1) + (y & 1)) & 0x7;

                int column_location = posY * height * 2 + ((x + swap_selector) & 0x7) * 4;

                int byte_num = (x >> 3) & 3;
                int bits_set = (y >> 1) & 1;

                int pos = page_location + block_location + column_location + byte_num;

                // Write to swizzled texels (pack two 4bpp pixels per byte)
                if (pos < swizzledTexels.Length)
                {
                    byte swizzledByte = swizzledTexels[pos];

                    if ((bits_set & 1) != 0)
                    {
                        // High nibble
                        swizzledTexels[pos] = (byte)((swizzledByte & 0x0F) | (uPen << 4));
                    }
                    else
                    {
                        // Low nibble
                        swizzledTexels[pos] = (byte)((swizzledByte & 0xF0) | (uPen & 0x0F));
                    }
                }
            }
        }

        return swizzledTexels;
    }
    
    // Todo: Document
    public static byte[] Unswizzle4bpp(byte[] pInTexels, int width, int height)
    {
        byte[] pSwizTexels = new byte[width * height / 2];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;

                // Unswizzling calculations
                int pageX = x & (~0x7f);
                int pageY = y & (~0x7f);

                int pages_horz = (width + 127) / 128;
                int pages_vert = (height + 127) / 128;

                int page_number = (pageY / 128) * pages_horz + (pageX / 128);

                int page32Y = (page_number / pages_vert) * 32;
                int page32X = (page_number % pages_vert) * 64;

                int page_location = page32Y * height * 2 + page32X * 4;

                int locX = x & 0x7f;
                int locY = y & 0x7f;

                int block_location = ((locX & (~0x1f)) >> 1) * height + (locY & (~0xf)) * 2;
                int swap_selector = (((y + 2) >> 2) & 0x1) * 4;
                int posY = (((y & (~3)) >> 1) + (y & 1)) & 0x7;

                int column_location = posY * height * 2 + ((x + swap_selector) & 0x7) * 4;

                int byte_num = (x >> 3) & 3;
                int bits_set = (y >> 1) & 1;

                int pos = page_location + block_location + column_location + byte_num;

                if (pos < pInTexels.Length)
                {
                    int uPen;
                    if ((bits_set & 1) != 0)
                    {
                        uPen = (pInTexels[pos] >> 4) & 0xf;
                    }
                    else
                    {
                        uPen = pInTexels[pos] & 0xf;
                    }

                    int byteIndex = index >> 1;
                    byte pix = pSwizTexels[byteIndex];

                    if ((index & 1) != 0)
                    {
                        pSwizTexels[byteIndex] = (byte)(((uPen << 4) & 0xf0) | (pix & 0x0f));
                    }
                    else
                    {
                        pSwizTexels[byteIndex] = (byte)((pix & 0xf0) | (uPen & 0x0f));
                    }
                }
            }
        }

        return pSwizTexels;
    }
    
    // Todo: Document
    public static byte[] SwizzlePalette(byte[] palBuffer, int width)
    {
        byte[] swizzledPal = new byte[1024];

        for (int p = 0; p < width; p++)
        {
            int pos = (p & 231) + ((p & 8) << 1) + ((p & 16) >> 1); // same swizzle index
            int destIndex = p * 4;
            int srcIndex = pos * 4;

            Array.Copy(palBuffer, srcIndex, swizzledPal, destIndex, 4);
        }

        return swizzledPal;
    }
    
    // Todo: Document
    public static byte[] UnswizzlePalette(byte[] palBuffer, int width)
    {
        byte[] newPal = new byte[1024];
        for (int p = 0; p < width; p++)
        {
            int pos = (p & 231) + ((p & 8) << 1) + ((p & 16) >> 1);
            int srcIndex = p * 4;
            int destIndex = pos * 4;

            Array.Copy(palBuffer, srcIndex, newPal, destIndex, 4);
        }
        return newPal;
    }
   
    // Todo: Document
    public static float UintByteToFloat(int Int)
    {
        return BitConverter.ToSingle(BitConverter.GetBytes(Int), 0);
    }
}