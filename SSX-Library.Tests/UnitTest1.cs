// using SSX_Library.Internal;
using SSX_Library;

// using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {        
        // string input = "/home/eric/Downloads/speech.big";
        // string output = "/home/eric/Downloads/sus";
        // BIG.Extract(input, output);

        // string input = "/home/eric/Downloads/sus/data/speech/anim/headers.big";
        // string output = "/home/eric/Downloads/ass";
        // BIG.Extract(input, output);

        string input = "/home/eric/Downloads/sus/data/speech/char/01a1.dat";
        string output = "/home/eric/Downloads/ass";
        string tools = "/home/eric/Downloads";
        // BIG.Extract(input, output);
        Internal.Audio.DAT.Extract(tools, input, output);



    }
}