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
        // byte[] numbers = [0b0000_0001, 0b0000_0100, 0b0000_1100, 0b0000_0000];
        byte[] numbers = [1, 2, 3, 4, 5, 6, 7];
        byte[] numbers2 = [3];
        byte Byte = 0b1011_0010;
        var output = ByteConv.ByteBitSwap(Byte, 7, 0);
        var output1 = ByteConv.ByteBitSwap(output, 4, 2);
        var output2 = ByteConv.ByteBitSwap(output1, 5, 3);

        Console.WriteLine(Convert.ToString(output2, 2).PadLeft(8, '0'));
    }
}