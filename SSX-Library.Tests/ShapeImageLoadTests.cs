using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.EATextureLibrary;

namespace SSX_Library.Tests;

/// <summary>Opaque PNGs normally decode as Rgb24; every SSH loader must convert them to its Rgba32 store.</summary>
public class ShapeImageLoadTests
{
    private static string WriteRgbPng()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ssx-rgb-{Guid.NewGuid():N}.png");
        using var image = new Image<Rgb24>(2, 1);
        image[0, 0] = new Rgb24(12, 34, 56);
        image[1, 0] = new Rgb24(210, 180, 90);
        image.SaveAsPng(path);
        return path;
    }

    private static void AssertConverted(Image<Rgba32> image)
    {
        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(new Rgba32(12, 34, 56, 255), image[0, 0]);
        Assert.Equal(new Rgba32(210, 180, 90, 255), image[1, 0]);
    }

    [Fact]
    public void OldShapeAddImage_ConvertsRgbPngToRgba()
    {
        string path = WriteRgbPng();
        try
        {
            var handler = new OldShapeHandler();
            handler.AddImage(OldShapeHandler.MatrixType.EightBit, "0000", path);
            AssertConverted(handler.ShapeImages[0].Image);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OldShapeLoadSingleImage_ConvertsRgbPngToRgba()
    {
        string path = WriteRgbPng();
        try
        {
            var handler = new OldShapeHandler();
            handler.ShapeImages.Add(new OldShapeHandler.ShapeImage { Image = new Image<Rgba32>(1, 1) });
            handler.LoadSingleImage(path, 0);
            AssertConverted(handler.ShapeImages[0].Image);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NewShapeLoadSingleImage_ConvertsRgbPngToRgba()
    {
        string path = WriteRgbPng();
        try
        {
            var handler = new NewShapeHandler();
            handler.ShapeImages.Add(new NewShapeHandler.ShapeImage { Image = new Image<Rgba32>(1, 1) });
            handler.LoadSingleImage(path, 0);
            AssertConverted(handler.ShapeImages[0].Image);
        }
        finally { File.Delete(path); }
    }
}
