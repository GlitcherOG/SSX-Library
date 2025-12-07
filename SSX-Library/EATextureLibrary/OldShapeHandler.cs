using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.FileHandlers;
using SSXLibrary.Utilities;
using System.Text;


namespace SSX_Library.EATextureLibrary
{
    public class OldShapeHandler
    {
        public string MagicWord;
        public int FileSize;
        public int ImageCount;
        public string Format;
        public string Group;
        public string EndingString;
        public List<ShapeImage> ShapeImages = new List<ShapeImage>();

        public void LoadShape(string path)
        {
            ShapeImages = new List<ShapeImage>();
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MagicWord = StreamUtil.ReadString(stream, 4);

                if (MagicWord == "SHPS")
                {
                    FileSize = StreamUtil.ReadUInt32(stream);

                    ImageCount = StreamUtil.ReadUInt32(stream);

                    Format = StreamUtil.ReadString(stream, 4);

                    for (int i = 0; i < ImageCount; i++)
                    {
                        ShapeImage tempImage = new ShapeImage();

                        tempImage.Shortname = StreamUtil.ReadString(stream, 4);

                        tempImage.Offset = StreamUtil.ReadUInt32(stream);

                        ShapeImages.Add(tempImage);
                    }

                    Group = StreamUtil.ReadString(stream, 4);

                    EndingString = StreamUtil.ReadString(stream, 4);

                    LoadImages(stream);
                }
                else
                {
                    Console.WriteLine(MagicWord + " Unsupported format");
                }
                stream.Dispose();
                stream.Close();
            }
        }

        private void LoadImages(Stream stream)
        {
            for (int i = 0; i < ShapeImages.Count; i++)
            {
                ShapeImage tempImage = ShapeImages[i];
                stream.Position = tempImage.Offset;

                tempImage.ShapeHeaders = new List<ShapeHeader>();

                while (stream.Position < tempImage.Offset)
                {
                    var shape = new ShapeHeader();

                    shape.MatrixFormat = (MatrixType)StreamUtil.ReadUInt8(stream);

                    shape.Size = StreamUtil.ReadInt24(stream);

                    shape.Width = StreamUtil.ReadInt16(stream);

                    shape.Height = StreamUtil.ReadInt16(stream);

                    shape.Xaxis = StreamUtil.ReadInt16(stream);

                    shape.Yaxis = StreamUtil.ReadInt16(stream);

                    //Add Other Flags Later
                    shape.Flags = StreamUtil.ReadInt32(stream);

                    if (shape.Size == 0 || shape.MatrixFormat == MatrixType.LongName)
                    {
                        int RealSize = shape.Width * shape.Height;
                        if (shape.MatrixFormat == MatrixType.LongName)
                        {
                            RealSize = RealSize * 4;
                        }

                        shape.Matrix = StreamUtil.ReadBytes(stream, RealSize);
                    }
                    else
                    {
                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.Size - 16);
                    }

                    tempImage.ShapeHeaders.Add(shape);
                }

                //Get Matrix Type
                tempImage.MatrixType = GetShapeMatrixType(tempImage);
                var imageMatrix = GetShapeHeader(tempImage, tempImage.MatrixType);

                tempImage.SwizzledImage = (imageMatrix.Flags & 8192) == 8192;

                //Uncompress
                if (MatrixType.EightBitCompressed == tempImage.MatrixType)
                {
                    imageMatrix.Matrix = RefpackHandler.Decompress(imageMatrix.Matrix);
                }

                //Process Colors
                //Todo Check If Type is here instead
                if (tempImage.MatrixType == MatrixType.FourBit || tempImage.MatrixType == MatrixType.EightBit)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet);
                    tempImage.colorsTable = GetColorTable(tempImage);
                    tempImage = AlphaFix(tempImage);
                }

                //Process into image
                switch (tempImage.MatrixType)
                {
                    case MatrixType.FourBit:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle4bpp(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        }
                        tempImage.Image = EADecode.DecodeMatrix1(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.Width, imageMatrix.Height);
                        break;
                    case MatrixType.EightBit:
                    case MatrixType.EightBitCompressed:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle8(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        }
                        tempImage.Image = EADecode.DecodeMatrix2(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.Width, imageMatrix.Height);
                        break;
                    case MatrixType.FullColor:
                        tempImage.Image = EADecode.DecodeMatrix5(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    default:
                        Console.WriteLine(tempImage.MatrixType + " Unknown Matrix");
                        break;
                }

                //Metal Bin



                var longNameShape = GetShapeHeader(tempImage, MatrixType.LongName);
                if (longNameShape.MatrixFormat != 0)
                {
                    tempImage.Longname = Encoding.ASCII.GetString(longNameShape.Matrix).Replace("\0", "");
                }

                ShapeImages[i] = tempImage;
            }
        }
        private List<Rgba32> GetColorTable(ShapeImage newSSHImage)
        {
            var colorShape = GetShapeHeader(newSSHImage, MatrixType.FullColor);
            List<Rgba32> colors = new List<Rgba32>();

            for (int i = 0; i < colorShape.Width * colorShape.Height; i++)
            {
                colors.Add(new Rgba32(colorShape.Matrix[i * 4], colorShape.Matrix[i * 4 + 1], colorShape.Matrix[i * 4 + 2], colorShape.Matrix[i * 4 + 3]));
            }

            return colors;
        }

        private ShapeImage AlphaFix(ShapeImage newSSHImage)
        {
            bool TestAlpha = true;

            for (int i = 0; i < newSSHImage.colorsTable.Count; i++)
            {
                if (newSSHImage.colorsTable[i].A > 0x80)
                {
                    TestAlpha = false;
                    break;
                }
            }
            newSSHImage.AlphaFix = true;

            if (TestAlpha)
            {
                for (int i = 0; i < newSSHImage.colorsTable.Count; i++)
                {
                    var TempColour = newSSHImage.colorsTable[i];
                    int A = TempColour.A * 2;
                    if (A > 255)
                    {
                        A = 255;
                    }
                    TempColour = new Rgba32(TempColour.R, TempColour.G, TempColour.B, A);
                    newSSHImage.colorsTable[i] = TempColour;
                }
            }
            return newSSHImage;
        }

        private ShapeHeader GetShapeHeader(ShapeImage newSSHImage, MatrixType Type)
        {
            for (int i = 0; i < newSSHImage.ShapeHeaders.Count; i++)
            {
                if (newSSHImage.ShapeHeaders[i].MatrixFormat == Type)
                {
                    return newSSHImage.ShapeHeaders[i];
                }
            }
            return new ShapeHeader();
        }

        private MatrixType GetShapeMatrixType(ShapeImage tempImage)
        {
            for (int i = 0; i < tempImage.ShapeHeaders.Count; i++)
            {
                if (tempImage.ShapeHeaders[i].MatrixFormat == MatrixType.FourBit || tempImage.ShapeHeaders[i].MatrixFormat == MatrixType.EightBit || 
                    tempImage.ShapeHeaders[i].MatrixFormat == MatrixType.FullColor || tempImage.ShapeHeaders[i].MatrixFormat == MatrixType.EightBitCompressed)
                {
                    return tempImage.ShapeHeaders[i].MatrixFormat;
                }
            }

            return tempImage.ShapeHeaders[0].MatrixFormat;
        }

        public void ExtractImage(string path)
        {
            for (int i = 0; i < ShapeImages.Count; i++)
            {
                ShapeImages[i].Image.SaveAsPng(System.IO.Path.Combine(path, ShapeImages[i].Shortname + i + ".png"));
            }
        }

        public void ExtractSingleImage(string path, int i)
        {
            ShapeImages[i].Image.SaveAsPng(path);
        }

        public void LoadSingleImage(string path, int i)
        {
            var temp = ShapeImages[i];
            temp.Image = (Image<Rgba32>)Image.Load(path);
            temp.colorsTable = ImageUtil.GetBitmapColorsFast(temp.Image).ToList();
            temp.MatrixType = ShapeImages[i].MatrixType;
            ShapeImages[i] = temp;
        }

        public struct ShapeImage
        {
            public int Offset;
            public int Size;
            public string Shortname;
            public string Longname;
            public List<ShapeHeader> ShapeHeaders;

            //Converted
            public List<Rgba32> colorsTable;
            public Image<Rgba32> Image;
            public Image<A8> Metal;
            public int Unknown;
            public MatrixType MatrixType;
            public bool SwizzledImage;
            public bool SwizzledColours;
            public bool AlphaFix;
        }

        public struct ShapeHeader
        {
            public MatrixType MatrixFormat;
            public int Size;
            public int Width;
            public int Height;
            public int Xaxis;
            public int Yaxis;
            public int Flags;

            public byte[] Matrix;
        }

        public enum MatrixType : byte
        {
            Unknown = 0,
            FourBit = 1,
            EightBit = 2,
            FullColor = 5,

            ColorPallet = 33,
            MetalAlpha = 105,
            LongName = 111,

            EightBitCompressed = 130,
        }
    }
}
