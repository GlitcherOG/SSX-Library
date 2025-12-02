using System.Numerics;
using SSX_Library.Utilities;
using SSXLibrary.Utilities;
namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        byte number = 0b0011_1010;
        Assert.Equal(ByteUtil.ByteToBitConvert(number, 4, 7), ByteConv.GetByteNibble(number, ByteConv.Nibble.High));
    }

    [Fact]
    public void Test2()
    {
        var highest = Vector3.Max(Vector3.Zero, Vector3.One);
        Console.WriteLine(highest);
    }
}