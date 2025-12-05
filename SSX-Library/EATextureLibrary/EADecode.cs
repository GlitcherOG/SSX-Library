using SixLabors.ImageSharp;
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
            Image<Rgba32> NewImage = new Image<Rgba32>(width, height);

            int pos = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    NewImage[x, y] = new Rgba32(matrix[pos * 4], matrix[pos * 4 + 1], matrix[pos * 4 + 2], matrix[pos * 4 + 3]);
                    pos++;
                }
            }

            return NewImage;
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
