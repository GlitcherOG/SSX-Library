using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.Internal.Utilities;
using System.Collections.Generic;
using System.Text;

namespace SSX_Library.EATextureLibrary
{
    internal class EAEncode
    {
        //PS2
        //1 (4 Bit, 16 Colour Index)
        public static (byte[] Matrix, List<Rgba32> ColourTable) EncodeMatrix1(Image<Rgba32> image)
        {
            List<Rgba32> colourTable = new List<Rgba32>();

            byte[] TempMatrix = new byte[image.Height * image.Width];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int index = colourTable.IndexOf(image[x, y]); // Check if the item exists

                    if (index == -1) // If the item is not found
                    {
                        colourTable.Add(image[x, y]); // Add the item to the list
                        index = colourTable.Count - 1; // Get the index of the newly added item
                    }

                    TempMatrix[y * image.Width + x] = (byte)index;
                }
            }

            int MatrixSize = StreamUtil.AlignbyMath(TempMatrix.Length / 2, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int i = 0; i < TempMatrix.Length / 2; i++)
            {
                Matrix[i] = (byte)ByteUtil.BitConbineConvert(TempMatrix[i * 2], TempMatrix[i * 2 + 1], 0, 4, 4);
            }

            return (TempMatrix, colourTable);
        }
        //2 (8 Bit, 256 Colour Index)
        //123 Xbox (8 Bit, 256 Colour Index)
        //25 GC (8 bit, BGR5A3 Colour Index)
        public static (byte[] Matrix, List<Rgba32> ColourTable) EncodeMatrix2(Image<Rgba32> image)
        {
            List<Rgba32> colourTable = new List<Rgba32>();

            byte[] TempMatrix = new byte[image.Height * image.Width];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int index = colourTable.IndexOf(image[x, y]); // Check if the item exists

                    if (index == -1) // If the item is not found
                    {
                        colourTable.Add(image[x, y]); // Add the item to the list
                        index = colourTable.Count - 1; // Get the index of the newly added item
                    }

                    TempMatrix[y * image.Width + x] = (byte)index;
                }
            }

            return (TempMatrix, colourTable);
        }
        //5 (Full Colour)
        public static byte[] EncodeMatrix5(Image<Rgba32> image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(image.Height * image.Width * 4, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var Pixel = image[x, y];
                    int RowID = (y * image.Width*4) + (x * 4);
                    Matrix[RowID] = Pixel.R;
                    Matrix[RowID + 1] = Pixel.G;
                    Matrix[RowID + 2] = Pixel.B;
                    Matrix[RowID + 3] = Pixel.A;
                }
            }

            return Matrix;
        }

        //Nintendo Wii/GC
        //30 - N64 CMPR
        public static byte[] EncodeMatrix30(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;

            int blocksX = (width + 3) / 4;
            int blocksY = (height + 3) / 4;

            byte[] output = new byte[blocksX * blocksY * 8];

            int offset = 0;

            // N64 CMPR is arranged in 8x8 macroblocks.
            for (int macroY = 0; macroY < height; macroY += 8)
            {
                for (int macroX = 0; macroX < width; macroX += 8)
                {
                    EncodeBlock(
                        image,
                        macroX + 0,
                        macroY + 0,
                        output,
                        ref offset);

                    EncodeBlock(
                        image,
                        macroX + 4,
                        macroY + 0,
                        output,
                        ref offset);

                    EncodeBlock(
                        image,
                        macroX + 0,
                        macroY + 4,
                        output,
                        ref offset);

                    EncodeBlock(
                        image,
                        macroX + 4,
                        macroY + 4,
                        output,
                        ref offset);
                }
            }

            return output;
        }

        private static void EncodeBlock(
            Image<Rgba32> image,
            int startX,
            int startY,
            byte[] output,
            ref int offset)
        {
            Rgba32[] pixels = new Rgba32[16];

            // Read 4x4 pixels.
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int px = startX + x;
                    int py = startY + y;

                    if (px < image.Width && py < image.Height)
                    {
                        pixels[y * 4 + x] =
                            image[px, py];
                    }
                    else
                    {
                        // Edge padding.
                        pixels[y * 4 + x] =
                            new Rgba32(0, 0, 0, 0);
                    }
                }
            }

            // Find RGB565 endpoints.
            FindEndpoints(
                pixels,
                out ushort color0,
                out ushort color1);

            // Write RGB565 endpoints, big endian.
            output[offset + 0] =
                (byte)(color0 >> 8);

            output[offset + 1] =
                (byte)(color0 & 0xFF);

            output[offset + 2] =
                (byte)(color1 >> 8);

            output[offset + 3] =
                (byte)(color1 & 0xFF);

            // Generate palette.
            Rgba32[] palette = new Rgba32[4];

            palette[0] = DecodeRGB565(color0);
            palette[1] = DecodeRGB565(color1);

            if (color0 > color1)
            {
                palette[2] = Interpolate(
                    palette[0],
                    palette[1],
                    2,
                    1);

                palette[3] = Interpolate(
                    palette[0],
                    palette[1],
                    1,
                    2);
            }
            else
            {
                palette[2] = Interpolate(
                    palette[0],
                    palette[1],
                    1,
                    1);

                palette[3] =
                    new Rgba32(0, 0, 0, 0);
            }

            // Find the closest palette entry for each pixel.
            uint indices = 0;

            for (int i = 0; i < 16; i++)
            {
                int index = FindClosestColor(
                    pixels[i],
                    palette);

                indices |=
                    (uint)index << (30 - i * 2);
            }

            // Write indices.
            output[offset + 4] =
                (byte)(indices >> 24);

            output[offset + 5] =
                (byte)(indices >> 16);

            output[offset + 6] =
                (byte)(indices >> 8);

            output[offset + 7] =
                (byte)indices;

            offset += 8;
        }

        private static void FindEndpoints(
            Rgba32[] pixels,
            out ushort color0,
            out ushort color1)
        {
            int minR = 255;
            int minG = 255;
            int minB = 255;

            int maxR = 0;
            int maxG = 0;
            int maxB = 0;

            foreach (Rgba32 pixel in pixels)
            {
                minR = Math.Min(minR, pixel.R);
                minG = Math.Min(minG, pixel.G);
                minB = Math.Min(minB, pixel.B);

                maxR = Math.Max(maxR, pixel.R);
                maxG = Math.Max(maxG, pixel.G);
                maxB = Math.Max(maxB, pixel.B);
            }

            color0 = EncodeRGB565(
                (byte)maxR,
                (byte)maxG,
                (byte)maxB);

            color1 = EncodeRGB565(
                (byte)minR,
                (byte)minG,
                (byte)minB);

            // Force four-color BC1 mode.
            if (color0 <= color1)
            {
                (color0, color1) =
                    (color1, color0);
            }
        }

        private static ushort EncodeRGB565(
            byte r,
            byte g,
            byte b)
        {
            int r5 = (r * 31 + 127) / 255;
            int g6 = (g * 63 + 127) / 255;
            int b5 = (b * 31 + 127) / 255;

            return (ushort)(
                (r5 << 11) |
                (g6 << 5) |
                b5);
        }

        private static Rgba32 DecodeRGB565(
            ushort value)
        {
            int r = (value >> 11) & 0x1F;
            int g = (value >> 5) & 0x3F;
            int b = value & 0x1F;

            return new Rgba32(
                (byte)((r << 3) | (r >> 2)),
                (byte)((g << 2) | (g >> 4)),
                (byte)((b << 3) | (b >> 2)),
                255);
        }

        private static Rgba32 Interpolate(
            Rgba32 a,
            Rgba32 b,
            int aWeight,
            int bWeight)
        {
            int divisor = aWeight + bWeight;

            return new Rgba32(
                (byte)((a.R * aWeight +
                        b.R * bWeight) / divisor),

                (byte)((a.G * aWeight +
                        b.G * bWeight) / divisor),

                (byte)((a.B * aWeight +
                        b.B * bWeight) / divisor),

                255);
        }

        private static int FindClosestColor(
            Rgba32 pixel,
            Rgba32[] palette)
        {
            int bestIndex = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                int dr = pixel.R - palette[i].R;
                int dg = pixel.G - palette[i].G;
                int db = pixel.B - palette[i].B;

                int distance =
                    dr * dr +
                    dg * dg +
                    db * db;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        //Xbox
        //96 - BCnEncoder.Shared.CompressionFormat.Bc1
        public static byte[] EncodeMatrixDXT1(Image<Rgba32> image)
        {
            BcEncoder bcEncoder = new BcEncoder();

            bcEncoder.OutputOptions.GenerateMipMaps = false;
            bcEncoder.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bc1;

            byte [] Matrix = new byte[image.Width * image.Height * 4];

            int offset = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];

                    Matrix[offset++] = pixel.R;
                    Matrix[offset++] = pixel.G;
                    Matrix[offset++] = pixel.B;
                    Matrix[offset++] = pixel.A;
                }
            }

            var Matrixes = bcEncoder.EncodeToRawBytes(
                Matrix,
                image.Width,
                image.Height,
                PixelFormat.Rgba32
            );

            return Matrixes.SelectMany(x => x).ToArray();
        }

        //97 - BCnEncoder.Shared.CompressionFormat.Bc2
        public static byte[] EncodeMatrix97(Image<Rgba32> image)
        {
            BcEncoder bcEncoder = new BcEncoder();

            bcEncoder.OutputOptions.GenerateMipMaps = false;
            bcEncoder.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bc2;

            byte[] Matrix = new byte[image.Width * image.Height * 4];

            int offset = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];

                    Matrix[offset++] = pixel.R;
                    Matrix[offset++] = pixel.G;
                    Matrix[offset++] = pixel.B;
                    Matrix[offset++] = pixel.A;
                }
            }

            var Matrixes = bcEncoder.EncodeToRawBytes(
                Matrix,
                image.Width,
                image.Height,
                PixelFormat.Rgba32
            );

            return Matrixes.SelectMany(x => x).ToArray();
        }

        //98 - BCnEncoder.Shared.CompressionFormat.Bc3
        public static byte[] EncodeMatrix98(Image<Rgba32> image)
        {
            BcEncoder bcEncoder = new BcEncoder();

            bcEncoder.OutputOptions.GenerateMipMaps = false;
            bcEncoder.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bc3;

            byte[] Matrix = new byte[image.Width * image.Height * 4];

            int offset = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];

                    Matrix[offset++] = pixel.R;
                    Matrix[offset++] = pixel.G;
                    Matrix[offset++] = pixel.B;
                    Matrix[offset++] = pixel.A;
                }
            }

            var Matrixes = bcEncoder.EncodeToRawBytes(
                Matrix,
                image.Width,
                image.Height,
                PixelFormat.Rgba32
            );

            return Matrixes.SelectMany(x => x).ToArray();
        }

        //109 - ImageFormats.BGRA4444 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L289
        public static byte[] EncodeMatrix109(Image<Rgba32> Image)
        {
            //Process Image
            byte[] Matrix = new byte[Image.Width*Image.Height*2];
            Image.CloneAs<Bgra4444>().CopyPixelDataTo(Matrix);
            return Matrix;
        }


        //120 - ImageFormats.BGR565 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L311
        public static byte[] EncodeMatrix120(Image<Rgba32> Image)
        {
            //Process Image
            byte[] Matrix = new byte[Image.Width * Image.Height * 2];
            Image.CloneAs<Bgr565>().CopyPixelDataTo(Matrix);
            return Matrix;
        }

        //123 - Indexed Image https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L334
        //125 - BCnEncoder.Shared.CompressionFormat.Bgra
        public static byte[] EncodeMatrix125(Image<Rgba32> image)
        {
            BcEncoder bcEncoder = new BcEncoder();

            bcEncoder.OutputOptions.GenerateMipMaps = false;
            bcEncoder.OutputOptions.Format = BCnEncoder.Shared.CompressionFormat.Bgra;

            byte[] Matrix = new byte[image.Width * image.Height * 4];

            int offset = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];

                    Matrix[offset++] = pixel.R;
                    Matrix[offset++] = pixel.G;
                    Matrix[offset++] = pixel.B;
                    Matrix[offset++] = pixel.A;
                }
            }

            var Matrixes = bcEncoder.EncodeToRawBytes(
                Matrix,
                image.Width,
                image.Height,
                PixelFormat.Rgba32
            );

            return Matrixes.SelectMany(x => x).ToArray();
        }
    }
}
