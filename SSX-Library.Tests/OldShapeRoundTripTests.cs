using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.EATextureLibrary;

namespace SSX_Library.Tests;

public sealed class OldShapeRoundTripTests
{
    [Theory]
    [InlineData(OldShapeHandler.MatrixType.EightBit)]
    [InlineData(OldShapeHandler.MatrixType.EightBitCompressed)]
    [InlineData(OldShapeHandler.MatrixType.FullColor)]
    [InlineData(OldShapeHandler.MatrixType.BGRACompressed)]
    public void Ps2Texture_RoundTripsPixelsAndDirectory(OldShapeHandler.MatrixType matrixType)
    {
        using var image = new Image<Rgba32>(16, 16);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image[x, y] = new Rgba32((byte)(x * 13), (byte)((y % 4) * 51), 37, 255);

        WithRoundTrip(image, matrixType, alphaFix: false, (loaded, bytes) =>
        {
            Assert.Equal("G278", loaded.Format);
            Assert.Equal("0000", loaded.ShapeImages[0].Shortname);
            Assert.Equal("Buy ERTS", loaded.EndingString);
            Assert.Equal(bytes.Length, BitConverter.ToInt32(bytes, 4));
            Assert.Equal(matrixType, loaded.ShapeImages[0].MatrixType);
            var actual = loaded.ShapeImages[0].Image;
            Assert.Equal(image.Width, actual.Width);
            Assert.Equal(image.Height, actual.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    Assert.Equal(image[x, y], actual[x, y]);
        });
    }

    [Fact]
    public void Ps2EightBit_ShortPaletteKeepsRetailLayoutAndGsAlpha()
    {
        using var image = new Image<Rgba32>(16, 16);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image[x, y] = x % 2 == 0 ? new Rgba32(12, 34, 56, 255) : new Rgba32(98, 76, 54, 0);

        WithRoundTrip(image, OldShapeHandler.MatrixType.EightBit, alphaFix: true, (loaded, bytes) =>
        {
            int pageOffset = BitConverter.ToInt32(bytes, 20);
            int paletteOffset = pageOffset + 16 + image.Width * image.Height;
            Assert.Equal(33, bytes[paletteOffset]);
            Assert.Equal(256, BitConverter.ToInt16(bytes, paletteOffset + 4));
            Assert.Equal(128, bytes[paletteOffset + 16 + 3]);
            Assert.Equal(256, loaded.ShapeImages[0].colorsTable.Count);
            Assert.Equal(image[0, 0], loaded.ShapeImages[0].Image[0, 0]);
            Assert.Equal(image[1, 0], loaded.ShapeImages[0].Image[1, 0]);
        });
    }

    [Theory]
    [InlineData(OldShapeHandler.MatrixType.EightBit)]
    [InlineData(OldShapeHandler.MatrixType.EightBitCompressed)]
    public void Ps2EightBit_Over256ColoursPreservesTransparency(OldShapeHandler.MatrixType matrixType)
    {
        using var image = new Image<Rgba32>(64, 32);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image[x, y] = x < 32
                    ? new Rgba32(0, 0, 0, 0)
                    : new Rgba32((byte)(x * 4), (byte)(y * 8), (byte)(x + y), 255);

        WithRoundTrip(image, matrixType, alphaFix: true, (loaded, _) =>
        {
            Assert.InRange(loaded.ShapeImages[0].colorsTable.Count, 1, 256);
            Assert.Equal(0, loaded.ShapeImages[0].Image[0, 0].A);
            Assert.Equal(255, loaded.ShapeImages[0].Image[63, 31].A);
            Assert.True(loaded.ShapeImages[0].Image[63, 31].R > 0);
        });
    }

    private static void WithRoundTrip(Image<Rgba32> image, OldShapeHandler.MatrixType matrixType,
        bool alphaFix, Action<OldShapeHandler, byte[]> verify)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ssx-texture-{Guid.NewGuid():N}.ssh");
        var handler = new OldShapeHandler { Format = "G278" };
        handler.ShapeImages.Add(new OldShapeHandler.ShapeImage
        {
            Shortname = "0000", MatrixType = matrixType, Image = image, AlphaFix = alphaFix,
        });
        var loaded = new OldShapeHandler();
        try
        {
            handler.SaveShape(path);
            loaded.LoadShape(path);
            verify(loaded, File.ReadAllBytes(path));
        }
        finally
        {
            foreach (var shape in loaded.ShapeImages) shape.Image?.Dispose();
            File.Delete(path);
        }
    }
}
