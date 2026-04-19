using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SSX_Library.Internal.Utilities.StreamExtensions;
using CommunityToolkit.HighPerformance;

namespace SSX_Library.Internal.Textures;

internal static class Gizmondo
{
    public static void XgtToPng(string pixelDataPath, string palettePath, string outputPngPath)
    {
        // Read palette data
        using FileStream paletteFile = File.OpenRead(palettePath);
        List<byte> formattedPalette = ReadPalette(paletteFile);

        // Read Pixel data
        using FileStream pixelDataFile = File.OpenRead(pixelDataPath);

        // Find resolution
        uint resolution;
        if (pixelDataFile.Length > 0x10000 + 1024)
        {
            resolution = 256;
        }
        else
        {
            pixelDataFile.Seek(-1024, SeekOrigin.End);
            resolution = (uint)Math.Sqrt(pixelDataFile.Position);
        }

        // Create image
        pixelDataFile.Position = 0;
        IndicesToImage(pixelDataFile, formattedPalette, resolution, outputPngPath);
    }

    public static void XtfToPngs(string inputPath, string outputFolder)
    {
        // Read palette data
        using FileStream textureFile = File.OpenRead(inputPath);
        List<byte> formattedPalette = ReadPalette(textureFile);

        // Read Pixel data
        // Get mipmap level count
        textureFile.Position = 0x14;
        uint mipmapLevelCount = textureFile.ReadUInt32(ByteOrder.LittleEndian);

        // Read index arrays for each level, and turn them into images.
        textureFile.Position = 0x18;
        string filename = Path.GetFileNameWithoutExtension(inputPath);
        // 256 x 256
        if (mipmapLevelCount == 5)
        {
            IndicesToImage(textureFile, formattedPalette, 256, Path.Join([outputFolder, filename + "_256x256.png"]));
        }
        // 128 x 128
        if (mipmapLevelCount >= 4)
        {
            IndicesToImage(textureFile, formattedPalette, 128, Path.Join([outputFolder, filename + "_128x128.png"]));
        }
        // 64 x 64
        IndicesToImage(textureFile, formattedPalette, 64, Path.Join([outputFolder, filename + "_64x64.png"]));
        // 32 x 32
        IndicesToImage(textureFile, formattedPalette, 32, Path.Join([outputFolder, filename + "_32x32.png"]));
        // 16 x 16
        IndicesToImage(textureFile, formattedPalette, 16, Path.Join([outputFolder, filename + "_16x16.png"]));
    }

    private static List<byte> ReadPalette(Stream file)
    {
        file.Seek(-0x400, SeekOrigin.End);
        byte[] paletteColorData = file.ReadBytes(0x400);
        List<byte> formattedPalette = [];
        for (int i = 0; i < 0x100; i++)
        {
            // BGRA
            formattedPalette.Add(paletteColorData[i * 4 + 2]);
            formattedPalette.Add(paletteColorData[i * 4 + 1]);
            formattedPalette.Add(paletteColorData[i * 4]);
            formattedPalette.Add(paletteColorData[i * 4 + 3]);
        }
        return formattedPalette;
    }

    private static void IndicesToImage(Stream file, List<byte> palette, uint resolution, string outputPath)
    {
        List<Rgba32> realColors = [];
        for (int _ = 0; _ < resolution * resolution; _++)
        {
            int colorIndex = file.ReadByte();
            realColors.Add(new()
            {
                R = palette[colorIndex * 4],
                G = palette[colorIndex * 4 + 1],
                B = palette[colorIndex * 4 + 2],
                A = palette[colorIndex * 4 + 3]
            });
        }
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(realColors.AsSpan(), (int)resolution, (int)resolution);
        image.SaveAsPng(outputPath);
    }
}
