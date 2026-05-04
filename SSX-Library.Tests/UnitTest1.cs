// using SSX_Library.Internal;
// using SSX_Library;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {        
        string input = "/home/eric/Downloads/langhead.big";
        string output = "/home/eric/Downloads/output";
        BIG.Extract(input, output);
    }
}