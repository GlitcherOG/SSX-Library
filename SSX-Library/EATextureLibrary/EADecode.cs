using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.Internal.Utilities;

namespace SSX_Library.EATextureLibrary
{
    internal class EADecode
    {
        //PS2
        //1 (4 Bit, 16 Colour Index)
        public static Image<Rgba32> DecodeMatrix1(byte[] matrix, List<Rgba32> colour, int width, int height)
        {
            byte[] decodedBytes = new byte[matrix.Length * 2];
            int posPoint = 0;
            for (int a = 0; a < matrix.Length; a++)
            {
                decodedBytes[posPoint] = (byte)ByteUtil.ByteToBitConvert(matrix[a], 0, 3);
                posPoint++;
                decodedBytes[posPoint] = (byte)ByteUtil.ByteToBitConvert(matrix[a], 4, 7);
                posPoint++;
            }
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int colorPos = decodedBytes[x + width * y];
                    NewImage[x, y] = colour[colorPos];
                }
            }

            return NewImage;
        }


        //2 (8 Bit, 256 Colour Index)
        //123 Xbox (8 Bit, 256 Colour Index)
        public static Image<Rgba32> DecodeMatrix2(byte[] matrix, List<Rgba32> colour, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int colorPos = matrix[x + width * y];
                    NewImage[x, y] = colour[colorPos];
                }
            }

            return NewImage;
        }


        //5 (Full Colour)
        public static Image<Rgba32> DecodeMatrix5(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = Image.LoadPixelData<Rgba32>(matrix ,width, height);

            return NewImage;
        }

        //Nintendo Wii/GC
        //21
        public static Image<Rgba32> DecodeMatrix21(byte[] Matrix, int width, int height)
        {
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ushort value = (ushort)(
       (Matrix[(x+width * y) * 2] << 8) |
       Matrix[(x+width * y) * 2 + 1]);

                    byte r;
                    byte g;
                    byte b;
                    byte a;

                    if ((value & 0x8000) != 0)
                    {
                        // 1RRRRRGGGGGBBBBB
                        int r5 = (value >> 10) & 0x1F;
                        int g5 = (value >> 5) & 0x1F;
                        int b5 = value & 0x1F;

                        r = (byte)((r5 << 3) | (r5 >> 2));
                        g = (byte)((g5 << 3) | (g5 >> 2));
                        b = (byte)((b5 << 3) | (b5 >> 2));
                        a = 255;
                    }
                    else
                    {
                        // 0AAARRRRGGGGBBBB
                        int a3 = (value >> 12) & 0x07;
                        int r4 = (value >> 8) & 0x0F;
                        int g4 = (value >> 4) & 0x0F;
                        int b4 = value & 0x0F;

                        r = (byte)((r4 << 4) | r4);
                        g = (byte)((g4 << 4) | g4);
                        b = (byte)((b4 << 4) | b4);
                        a = (byte)((a3 << 5) | (a3 << 2) | (a3 >> 1));
                    }

                    NewImage[x, y] = new Rgba32(r, g, b, a);
                }
            }
            return NewImage;
        }

        //25
        //30
        public static Image<Rgba32> DecodeMatrix30(byte[] data,int width,int height)
        {
            int blocksX = (width + 3) / 4;
            int blocksY = (height + 3) / 4;

            int requiredSize = blocksX * blocksY * 8;

            if (data.Length < requiredSize)
            {
                throw new ArgumentException(
                    $"CMPR data is too small. " +
                    $"Expected at least {requiredSize} bytes, got {data.Length}.");
            }

            Image<Rgba32> image = new(width, height);

            int sourceOffset = 0;

            for (int macroY = 0; macroY < height; macroY += 8)
            {
                for (int macroX = 0; macroX < width; macroX += 8)
                {
                    DecodeBlock(data,ref sourceOffset,image,macroX + 0,macroY + 0);

                    DecodeBlock(data,ref sourceOffset,image,macroX + 4,macroY + 0);

                    DecodeBlock(data,ref sourceOffset,image,macroX + 0,macroY + 4);

                    DecodeBlock(data,ref sourceOffset,image,macroX + 4,macroY + 4);
                }
            }

            return image;
        }

        private static void DecodeBlock(
            byte[] data,
            ref int offset,
            Image<Rgba32> image,
            int startX,
            int startY)
        {
            if (offset + 8 > data.Length)
                return;

            // RGB565 colors are big-endian in the N64 CMPR data.
            ushort color0 = ReadUInt16BE(data, offset + 0);
            ushort color1 = ReadUInt16BE(data, offset + 2);

            /*
             * Four bytes containing 16 two-bit indices.
             *
             * Each pixel uses two bits:
             *
             * pixel 0 = bits 31-30
             * pixel 1 = bits 29-28
             * ...
             * pixel 15 = bits 1-0
             */
            uint indices =
                ((uint)data[offset + 4] << 24) |
                ((uint)data[offset + 5] << 16) |
                ((uint)data[offset + 6] << 8) |
                data[offset + 7];

            offset += 8;

            Rgba32[] colors = new Rgba32[4];

            colors[0] = DecodeRGB565(color0);
            colors[1] = DecodeRGB565(color1);

            if (color0 > color1)
            {
                // Four-color BC1 mode.
                colors[2] = Interpolate(
                    colors[0],
                    colors[1],
                    2,
                    1);

                colors[3] = Interpolate(
                    colors[0],
                    colors[1],
                    1,
                    2);
            }
            else
            {
                // Three-color BC1 mode.
                colors[2] = Interpolate(
                    colors[0],
                    colors[1],
                    1,
                    1);

                colors[3] = new Rgba32(
                    0,
                    0,
                    0,
                    0);
            }

            // Decode the 4x4 pixels.
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int pixelIndex = y * 4 + x;

                    int colorIndex =
                        (int)((indices >> (30 - pixelIndex * 2)) & 3);

                    int destX = startX + x;
                    int destY = startY + y;

                    if (destX >= image.Width ||
                        destY >= image.Height)
                    {
                        continue;
                    }

                    image[destX, destY] = colors[colorIndex];
                }
            }
        }

        private static ushort ReadUInt16BE(
            byte[] data,
            int offset)
        {
            return (ushort)(
                (data[offset] << 8) |
                data[offset + 1]);
        }

        private static Rgba32 DecodeRGB565(
            ushort value)
        {
            int r = (value >> 11) & 0x1F;
            int g = (value >> 5) & 0x3F;
            int b = value & 0x1F;

            // Expand 5/6-bit channels to 8-bit.
            byte red = (byte)((r << 3) | (r >> 2));
            byte green = (byte)((g << 2) | (g >> 4));
            byte blue = (byte)((b << 3) | (b >> 2));

            return new Rgba32(
                red,
                green,
                blue,
                255);
        }

        private static Rgba32 Interpolate(
            Rgba32 a,
            Rgba32 b,
            int aWeight,
            int bWeight)
        {
            int divisor = aWeight + bWeight;

            byte r = (byte)(
                (a.R * aWeight +
                 b.R * bWeight) / divisor);

            byte g = (byte)(
                (a.G * aWeight +
                 b.G * bWeight) / divisor);

            byte blue = (byte)(
                (a.B * aWeight +
                 b.B * bWeight) / divisor);

            return new Rgba32(
                r,
                g,
                blue,
                255);
        }

        //Xbox
        //96 - BCnEncoder.Shared.CompressionFormat.Bc1
        public static Image<Rgba32> DecodeMatrixDXT1(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            BcDecoder bcDecoder = new BcDecoder();

            var Temp = bcDecoder.DecodeRaw(matrix, width, height, BCnEncoder.Shared.CompressionFormat.Bc1);

            int post = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    NewImage[x, y] = new Rgba32(Temp[post].r, Temp[post].g, Temp[post].b, Temp[post].a);
                    post++;
                }
            }

            return NewImage;
        }


        //97 - BCnEncoder.Shared.CompressionFormat.Bc2
        public static Image<Rgba32> DecodeMatrix97(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            BcDecoder bcDecoder = new BcDecoder();

            var Temp = bcDecoder.DecodeRaw(matrix, width, height, BCnEncoder.Shared.CompressionFormat.Bc2);

            int post = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    NewImage[x, y] = new Rgba32(Temp[post].r, Temp[post].g, Temp[post].b, Temp[post].a);
                    post++;
                }
            }

            return NewImage;
        }

        //98 - BCnEncoder.Shared.CompressionFormat.Bc3
        public static Image<Rgba32> DecodeMatrix98(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            BcDecoder bcDecoder = new BcDecoder();

            var Temp = bcDecoder.DecodeRaw(matrix, width, height, BCnEncoder.Shared.CompressionFormat.Bc3);

            int post = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    NewImage[x, y] = new Rgba32(Temp[post].r, Temp[post].g, Temp[post].b, Temp[post].a);
                    post++;
                }
            }

            return NewImage;
        }


        //109 - ImageFormats.BGRA4444
        public static Image<Rgba32> DecodeMatrix109(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Bgra4444> NewImage = Image.LoadPixelData<Bgra4444>(matrix, width, height);

            return NewImage.CloneAs<Rgba32>();
        }


        //120 - ImageFormats.BGR565
        public static Image<Rgba32> DecodeMatrix120(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Bgr565> NewImage = Image.LoadPixelData<Bgr565>(matrix, width, height);

            return NewImage.CloneAs<Rgba32>();
        }

        //125 - BCnEncoder.Shared.CompressionFormat.Bgra
        public static Image<Rgba32> DecodeMatrix125(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            BcDecoder bcDecoder = new BcDecoder();

            var Temp = bcDecoder.DecodeRaw(matrix, width, height, BCnEncoder.Shared.CompressionFormat.Bgra);

            int post = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    NewImage[x, y] = new Rgba32(Temp[post].r, Temp[post].g, Temp[post].b, Temp[post].a);
                    post++;
                }
            }

            return NewImage;
        }

        //Nintendo Wii/GC
    }
}
