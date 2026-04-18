// using SSX_Library.Internal;
using SSX_Library;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        string inputPath = "/home/eric/Projects/SSX3 Gizmondo/Extracted Assets/tracks/ABC1/abc1_terrain06.xtf";
        string outputFolder = "/home/eric/Downloads";
        XTF.ToPngs(inputPath, outputFolder);

        // string inputPath = "/home/eric/Projects/SSX3 Gizmondo/Extracted Assets/characters/zoe_02.xgt";
        // string inputPalettePath = "/home/eric/Projects/SSX3 Gizmondo/Extracted Assets/characters/zoe_02_low.xgt";
        // string outputfile = "/home/eric/Downloads/zoe.png";
        // XGT.ToPng(inputPath, inputPalettePath, outputfile);

    }
}