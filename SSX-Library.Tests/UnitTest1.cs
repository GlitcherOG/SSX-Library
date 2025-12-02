using SSX_Library.Utilities;
using SSXLibrary.Utilities;
namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        // byte number = 0b0011_1010;
        // Assert.Equal(ByteUtil.ByteToBitConvert(number, 4, 7), ByteConv.GetByteNibble(number, ByteConv.Nibble.High));
    }

    [Fact]
    public void Test2()
    {
//                                26         18 17          9 8            0
//                                |            ||            ||            | 
        byte[] numbers = [0b0000_0001, 0b0000_0100, 0b0000_1100, 0b0000_0000];


        Console.WriteLine(string.Join(", ", ByteConv.BytesToInt9Array(numbers, ByteConv.ByteOrder.BigEndian)));
        // Assert.Equal(1, ByteConv.BytesToInt9Array(numbers, ByteConv.ByteOrder.BigEndian)[0]);
        // Assert.Equal(1, ByteConv.BytesToInt9Array(numbers, ByteConv.ByteOrder.LittleEndian)[0]);
        // Assert.Equal(1, ByteConv.BytesToInt9Array(numbers, ByteConv.ByteOrder.LittleEndian)[0]);
        // Assert.Equal(1, ByteUtil.BytesToBitConvert(numbers, 0, 0 + 9));
        // Assert.Equal(1, ByteUtil.BytesToBitConvert(numbers, 10, 10 + 9));
        // Assert.Equal(1, ByteUtil.BytesToBitConvert(numbers, 20, 20 + 9));
    }
}