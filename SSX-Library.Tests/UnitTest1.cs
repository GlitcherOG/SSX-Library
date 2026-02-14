using SSX_Library.Internal;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        string bigPath = "/home/eric/Downloads/Refpack Bug/anm.big";
        string oldOutputPath = "/home/eric/Downloads/Refpack Bug/alaska_old";
        string newOutputPath = "/home/eric/Downloads/Refpack Bug/anm";

        string inputFolder = "/home/eric/Downloads/Refpack Bug/anm";
        string creationPath = "/home/eric/Downloads/Refpack Bug/anm_new.big";
        
        BIG.Create(BigType.C0FB, inputFolder, creationPath, true);

        // BIG.Extract(bigPath, newOutputPath);

    }
}