using System.Diagnostics;
using SSXLibrary;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var sample = File.ReadAllBytes("/home/eric/Downloads/Refpack_sample.bin");
        var output = SSXLibrary.FileHandlers.RefpackHandler.Decompress(sample);
        File.WriteAllBytes("/home/eric/Downloads/Decompressed_Refpack_sample.bin", output);
    }
}