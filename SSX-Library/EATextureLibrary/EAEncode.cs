using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.EATextureLibrary
{
    internal class EAEncode
    {
        //PS2
        //1 (4 Bit, 16 Colour Index)
        public static (byte[] Matrix, List<Rgba32> ColourTable) EncodeMatrix1(Image<Rgba32> image)
        {
            List<Rgba32>  colourTable = new List<Rgba32>();

            byte[] TempMatrix = new byte[image.Height * image.Width];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    TempMatrix[y * image.Width + x] = (byte)colourTable.IndexOf(image[x, y]);
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
        public static (byte[] Matrix, List<Rgba32> ColourTable) EncodeMatrix2(Image<Rgba32> image)
        {
            List<Rgba32> colourTable = new List<Rgba32>();

            byte[] TempMatrix = new byte[image.Height * image.Width];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    TempMatrix[y * image.Width + x] = (byte)colourTable.IndexOf(image[x, y]);
                }
            }

            int MatrixSize = StreamUtil.AlignbyMath(image.Height * image.Width, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Matrix[y * image.Width + x] = (byte)colourTable.IndexOf(image[x, y]);
                }
            }

            return (TempMatrix, colourTable);
        }
        //5 (Full Colour)
        public static byte[] EncodeMatrix5(Image<Rgba32> image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(image.Height * image.Width * 4, 16);

            MatrixSize = 16 - MatrixSize % 16;

            byte[] Matrix = new byte[MatrixSize];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var Pixel = image[x, y];
                    Matrix[y * x + x * 4] = Pixel.R;
                    Matrix[y * x + x * 4 + 1] = Pixel.G;
                    Matrix[y * x + x * 4 + 2] = Pixel.B;
                    Matrix[y * x + x * 4 + 3] = Pixel.A;
                }
            }

            return Matrix;
        }

        //130 is a header flag, will pull out so we can uncompress data easily
        //130 (8 bit, 256 Colour Index Compressed)

        //Xbox 360
        //96 - BCnEncoder.Shared.CompressionFormat.Bc1
        //97 - BCnEncoder.Shared.CompressionFormat.Bc2
        //109 - ImageFormats.BGRA4444 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L289
        //120 - ImageFormats.BGR565 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L311
        //123 - Indexed Image https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L334
        //125 - BCnEncoder.Shared.CompressionFormat.Bgra

        //Nintendo Wii/GC
    }
}
