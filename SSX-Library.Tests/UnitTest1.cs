using System.Numerics;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }

    [Fact]
    public void Test2()
    {
        var highest = Vector3.Max(Vector3.Zero, Vector3.One);
        Console.WriteLine(highest);
    }
}