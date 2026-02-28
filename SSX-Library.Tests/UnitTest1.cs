using SSX_Library.Internal;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        string bigPath = "/home/eric/Downloads/Refpack Bug/anm.big";
        string newOutputPath = "/home/eric/Downloads/Refpack Bug/anm";

        string inputFolder = "/home/eric/Downloads/Refpack Bug/anm";
        string creationPath = "/home/eric/Downloads/Refpack Bug/anm_new.big";
        
        // BIG.Create(BigType.BIGF, inputFolder, creationPath, true);

        // BIG.Extract(bigPath, newOutputPath);

        // foreach (var info in BIG.GetMembersInfo(bigPath))
        // {
        //     Console.WriteLine(info.Path);
        // }

    }
}