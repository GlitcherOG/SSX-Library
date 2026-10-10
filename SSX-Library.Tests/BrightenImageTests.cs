using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.EATextureLibrary;

namespace SSX_Library.Tests;

// Tests for OldShapeHandler.BrightenImage. A *2 brighten undoes the PS2 GS half-bright store
// (0x80 == 1.0). guard=false doubles every pixel (the level/skybox/crowd/board banks). guard=true
// doubles only when the opaque texels are all <= 0x80, leaving already-full-range images as-is; the
// PARTICLE bank uses it for its per-image mix of half-bright and full-range sprites. The guard
// mirrors AlphaFix.
public class BrightenImageTests
{
    private static OldShapeHandler HandlerWith(Image<Rgba32> img)
    {
        var h = new OldShapeHandler();
        h.ShapeImages.Add(new OldShapeHandler.ShapeImage { Image = img });
        return h;
    }

    [Fact]
    public void Guarded_HalfBrightImage_IsDoubled()
    {
        // Opaque interior capped at 0x80 == genuinely half-bright -> should brighten.
        var img = new Image<Rgba32>(2, 2);
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                img[x, y] = new Rgba32(128, 64, 32, 255);

        var h = HandlerWith(img);
        bool applied = h.BrightenImage(0, guard: true);

        Assert.True(applied);
        var p = h.ShapeImages[0].Image[0, 0];
        Assert.Equal(255, p.R); // clamp(128*2-1) saturates
        Assert.Equal(127, p.G); // 64*2-1
        Assert.Equal(63, p.B);  // 32*2-1
    }

    [Fact]
    public void Guarded_AlreadyFullRangeImage_IsLeftUntouched()
    {
        // A mid-tone opaque pixel (150 > 0x80) marks the image full-range; the guard leaves every
        // channel exactly as-is (an unguarded double would send 150 -> clamp(299) = 255).
        var img = new Image<Rgba32>(2, 2);
        img[0, 0] = new Rgba32(150, 100, 50, 255);
        img[1, 0] = new Rgba32(40, 40, 40, 255);
        img[0, 1] = new Rgba32(200, 10, 10, 255);
        img[1, 1] = new Rgba32(10, 10, 10, 255);

        var h = HandlerWith(img);
        bool applied = h.BrightenImage(0, guard: true);

        Assert.False(applied);
        Assert.Equal(new Rgba32(150, 100, 50, 255), h.ShapeImages[0].Image[0, 0]);
        Assert.Equal(new Rgba32(40, 40, 40, 255), h.ShapeImages[0].Image[1, 0]);
        Assert.Equal(new Rgba32(200, 10, 10, 255), h.ShapeImages[0].Image[0, 1]);
    }

    [Fact]
    public void Guarded_BrightRgbOnTransparentPixels_DoesNotFoolTheGuard()
    {
        // Only the OPAQUE texels gauge the stored range. A half-bright sprite with leftover/premultiplied
        // bright RGB behind fully-transparent pixels must still be recognised as half-bright and doubled.
        var img = new Image<Rgba32>(2, 2);
        img[0, 0] = new Rgba32(100, 100, 100, 255); // opaque, half-bright
        img[1, 0] = new Rgba32(120, 120, 120, 255); // opaque, half-bright
        img[0, 1] = new Rgba32(255, 255, 255, 0);   // transparent, bright RGB -> ignored
        img[1, 1] = new Rgba32(255, 255, 255, 0);   // transparent, bright RGB -> ignored

        var h = HandlerWith(img);
        bool applied = h.BrightenImage(0, guard: true);

        Assert.True(applied);
        Assert.Equal(199, h.ShapeImages[0].Image[0, 0].R); // 100*2-1, opaque pixel brightened
    }

    [Fact]
    public void Unguarded_DoublesEvenFullRange()
    {
        // The default (guard=false) doubles unconditionally, including an image whose opaque texels
        // exceed 0x80. This is the crowd/level/skybox/board path, where every frame doubles identically.
        var img = new Image<Rgba32>(1, 1);
        img[0, 0] = new Rgba32(150, 100, 50, 255); // would be "full-range" under the guard

        var h = HandlerWith(img);
        bool applied = h.BrightenImage(0); // no guard

        Assert.True(applied);
        var p = h.ShapeImages[0].Image[0, 0];
        Assert.Equal(255, p.R); // clamp(150*2-1) saturates
        Assert.Equal(199, p.G); // 100*2-1
        Assert.Equal(99, p.B);  // 50*2-1
    }
}
