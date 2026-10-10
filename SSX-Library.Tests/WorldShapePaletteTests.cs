using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData;

namespace SSX_Library.Tests;

/// <summary>
/// SSX 3 world shapes store an 8-bit palette in swizzled CLUT order, padded past the colour count the header
/// gives. Texture 479 on the retail disc lists 236 colours and carries 244 slots; colours 232-235 live in slots
/// 240-243, so a reader that stops at the count decodes them as transparent black specks.
/// </summary>
public class WorldShapePaletteTests
{
    private static byte[] Shape(int total, int storedSlots, int index, Func<int, Rgba32> slotColour)
    {
        const int width = 16, height = 16, size = 0x80 + width * height;
        var shape = new byte[size + 0x80 + storedSlots * 4];
        shape[0] = 2;                                          // 8-bit indexed
        shape[1] = size & 0xFF; shape[2] = size >> 8;           // u24 matrix block size
        shape[4] = width; shape[6] = height;
        Array.Fill(shape, (byte)index, 0x80, width * height);   // every texel the same index: swizzle-proof
        shape[size + 8] = (byte)total;                          // palette header: Total
        for (int slot = 0; slot < storedSlots; slot++)
        {
            var c = slotColour(slot);
            int at = size + 0x80 + slot * 4;
            (shape[at], shape[at + 1], shape[at + 2], shape[at + 3]) = (c.R, c.G, c.B, c.A);
        }
        return shape;
    }

    private static Rgba32 Load(byte[] shape)
    {
        var ssh = new WorldSSH();
        ssh.Load(new MemoryStream(shape));
        return ssh.bitmap[0, 0];
    }

    [Fact]
    public void AColourSwizzledIntoThePaddingIsRead()
    {
        // Ten colours padded to twenty slots: colour 8 swizzles to slot 16, past the count.
        byte[] shape = Shape(total: 10, storedSlots: 20, index: 8, slot => slot == 16 ? new Rgba32(90, 120, 150, 0x80) : new Rgba32(1, 1, 1, 0x80));

        Assert.Equal(new Rgba32(90, 120, 150, 255), Load(shape));
    }

    [Fact]
    public void AColourWithinTheCountIsUnchanged()
    {
        byte[] shape = Shape(total: 10, storedSlots: 20, index: 3, slot => slot == 3 ? new Rgba32(40, 50, 60, 0x80) : new Rgba32(1, 1, 1, 0x80));

        Assert.Equal(new Rgba32(40, 50, 60, 255), Load(shape));
    }
}
