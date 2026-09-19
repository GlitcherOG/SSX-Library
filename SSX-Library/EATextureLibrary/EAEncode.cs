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

        //109 - ImageFormats.BGRA4444 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L289
        //120 - ImageFormats.BGR565 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L311
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

        //Nintendo Wii/GC
    }
}
