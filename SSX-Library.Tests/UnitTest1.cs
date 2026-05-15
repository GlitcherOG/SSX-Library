// using SSX_Library.Internal;
using SSX_Library;

// using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        // Tricky
        string tools = "/home/eric/Downloads/audio_tools";
        string speech = "/home/eric/Downloads/tricky_extract/data/speech/";
        string animPath = speech + "anim";
        string charPath = speech + "char";
        string fePath = speech + "fe";
        string mcPath = speech + "mc";
        string narrPath = speech + "narr";
        string output = "/home/eric/Downloads/output";

        using var soundPacks = new SoundPacks(charPath, tools);

        // var names = soundPacks.GetSoundPacks();
        // Console.WriteLine($"{valid.Length}, {invalid.Length}");

        // foreach (var name in names)
        // {
        //     Console.WriteLine(name);
        // }



        // string input = "/home/eric/Downloads/3_english.big";
        // string output = "/home/eric/Downloads/english_extract";
        // BIG.Extract(input, output);
        // input = "/home/eric/Downloads/tricky_speech.big";
        // output = "/home/eric/Downloads/tricky_extract";
        // BIG.Extract(input, output);

    }
}