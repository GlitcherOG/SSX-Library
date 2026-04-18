using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System.Reflection;
using CommunityToolkit.HighPerformance;

namespace SSX_Library.Internal.Textures;

internal static class Gizmondo
{
    public static void XgtToPng(string pixelDataPath, string palettePath, string outputPngPath)
    {
        // Read palette data
        using FileStream paletteFile = File.OpenRead(palettePath);
        paletteFile.Seek(-1024, SeekOrigin.End);
        byte[] paletteColorData = paletteFile.ReadBytes(0x100 * 4);
        List<byte> formattedPaletteData = [];
        for (int i = 0; i < 0x100; i++)
        {
            // BGRA
            formattedPaletteData.Add(paletteColorData[i * 4 + 2]);
            formattedPaletteData.Add(paletteColorData[i * 4 + 1]);
            formattedPaletteData.Add(paletteColorData[i * 4]);
            formattedPaletteData.Add(paletteColorData[i * 4 + 3]);
        }

        // Read Pixel data
        using FileStream pixelDataFile = File.OpenRead(pixelDataPath);

        // Find resolution
        int resolution;
        if (pixelDataFile.Length > 0x10000 + 1024)
        {
            resolution = 256;
        }
        else
        {
            pixelDataFile.Seek(-1024, SeekOrigin.End);
            resolution = (int)Math.Sqrt(pixelDataFile.Position);
        }

        // Create image
        List<Rgba32> realColors = [];
        pixelDataFile.Position = 0;
        for (int _ = 0; _ < resolution * resolution; _++)
        {
            int colorIndex = pixelDataFile.ReadByte();
            realColors.Add(new()
            {
                R = formattedPaletteData[colorIndex * 4],
                G = formattedPaletteData[colorIndex * 4 + 1],
                B = formattedPaletteData[colorIndex * 4 + 2],
                A = formattedPaletteData[colorIndex * 4 + 3]
            });
        }
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(realColors.AsSpan(), resolution, resolution);
        image.SaveAsPng(outputPngPath);
    }
}
