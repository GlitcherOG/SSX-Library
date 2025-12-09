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
        EATextureLibrary.EANewShapeHandler eANewShapeHandler = new EATextureLibrary.EANewShapeHandler();

        eANewShapeHandler.LoadShape("G:\\SSX Modding\\disk\\SSX On Tour\\DATA\\TEXTURES\\Texture\\Full Range.ssh");

        eANewShapeHandler.ExtractImage("G:\\SSX Modding\\disk\\SSX On Tour\\DATA\\TEXTURES\\Texture\\Full Range");
    }
}