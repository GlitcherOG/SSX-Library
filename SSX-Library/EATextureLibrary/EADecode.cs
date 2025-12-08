using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.Utilities;

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
        //25
        //30
        public static Image<Rgba32> DecodeMatrix30(byte[] data, int width, int height)
        {
            var decoder = new BcDecoder();

            var img = new Image<Rgba32>(width, height);

            int tileCountX = width / 8;
            int tileCountY = height / 8;

            int offset = 0;

            for (int ty = 0; ty < tileCountY; ty++)
            {
                for (int tx = 0; tx < tileCountX; tx++)
                {
                    // four BC1 blocks per tile, each 8 bytes
                    DecodeBlock(decoder, img, data, ref offset, tx * 8, ty * 8);       // top-left
                    DecodeBlock(decoder, img, data, ref offset, tx * 8 + 4, ty * 8);   // top-right
                    DecodeBlock(decoder, img, data, ref offset, tx * 8, ty * 8 + 4);   // bottom-left
                    DecodeBlock(decoder, img, data, ref offset, tx * 8 + 4, ty * 8 + 4); // bottom-right
                }
            }

            return img;
        }

        private static void DecodeBlock(
            BcDecoder decoder,
            Image<Rgba32> output,
            byte[] data,
            ref int offset,
            int px,
            int py)
        {
            Span<byte> block = data.AsSpan(offset, 8);
            offset += 8;

            // decode BC1 block → ColorRgba32[16]
            var decoded = decoder.DecodeBlock(block, CompressionFormat.Bc1).Span;

            // Copy 4×4 block into output Memory2D<Rgba32>
            for (int y = 0; y < decoded.Height; y++)
            {
                if (py + y >= output.Height)
                    continue;

                var srcRow = decoded.GetRow(y);
                var dstRow = output.DangerousGetPixelRowMemory(py + y).Span;

                for (int x = 0; x < decoded.Width; x++)
                {
                    if (px + x >= output.Width)
                        continue;

                    ColorRgba32 c = srcRow[x];
                    dstRow[px + x] = new Rgba32(c.r, c.g, c.b, c.a);
                }
            }
        }

        //Xbox
        //96 - BCnEncoder.Shared.CompressionFormat.Bc1
        public static Image<Rgba32> DecodeMatrix96(byte[] matrix, int width, int height)
        {
            //Process Image
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            BcDecoder bcDecoder = new BcDecoder();

            var Temp = bcDecoder.DecodeRaw(matrix, width, height, BCnEncoder.Shared.CompressionFormat.Bc1);

            int post = 0;

            for (global::System.Int32 y = 0; y < height; y++)
            {
                for (global::System.Int32 x = 0; x < width; x++)
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

            for (global::System.Int32 y = 0; y < height; y++)
            {
                for (global::System.Int32 x = 0; x < width; x++)
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

            for (global::System.Int32 y = 0; y < height; y++)
            {
                for (global::System.Int32 x = 0; x < width; x++)
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
