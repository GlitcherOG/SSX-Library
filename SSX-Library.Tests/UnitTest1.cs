using SSX_Library.Internal;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        string bigPath = "/home/eric/Downloads/alaska.big";
        string oldOutputPath = "/home/eric/Downloads/alaska_old";
        string newOutputPath = "/home/eric/Downloads/alaska_new";

        string inputFolder = "/home/eric/Downloads/alaska";
        string creationPath = "/home/eric/Downloads/alaska_new.big";
        BIG.Create(BigType.C0FB, inputFolder, creationPath, true);


    }
}