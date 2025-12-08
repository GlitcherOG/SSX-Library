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
        public string EndingString;
        public List<ShapeImage> ShapeImages = new List<ShapeImage>();

        public void LoadShape(string path)
        {
            ShapeImages = new List<ShapeImage>();
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MagicWord = StreamUtil.ReadString(stream, 4);

                if (MagicWord == "SHPS" || MagicWord == "SHPX")
                {
                    FileSize = StreamUtil.ReadUInt32(stream);

                    ImageCount = StreamUtil.ReadUInt32(stream);

                    Format = StreamUtil.ReadString(stream, 4);

                    for (int i = 0; i < ImageCount; i++)
                    {
                        ShapeImage tempImage = new ShapeImage();

                        tempImage.Shortname = StreamUtil.ReadString(stream, 4);

                        tempImage.Offset = StreamUtil.ReadUInt32(stream);

                        //SSX OG Simple Check onsize should work

                        //SSX Tricky Requires each image being read correctly in terms of offset but for end it has Buy ERTS
                        //Buy ERTS for group ending

                        //SSX 3
                        //Mix of no ERTS for group ending and ERTS for group ending


                        ShapeImages.Add(tempImage);
                    }

                    for (global::System.Int32 i = 0; i < ShapeImages.Count; i++)
                    {
                        var TempImage = ShapeImages[i];

                        if(ShapeImages.Count-1!=i)
                        {
                            TempImage.Size = (int)(ShapeImages[i+1].Offset - TempImage.Offset);
                        }
                        else
                        {
                            TempImage.Size = (int)(stream.Length - TempImage.Offset);
                        }

                        int NewSize = (int)ByteUtil.FindPosition(stream, Encoding.ASCII.GetBytes("Buy ERTS"), TempImage.Offset, TempImage.Size);

                        if (NewSize != -1)
                        {
                            TempImage.Size = NewSize;
                        }



                        ShapeImages[i] = TempImage;
                    }

                    EndingString = StreamUtil.ReadString(stream, 8);

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

                while (stream.Position < tempImage.Offset + tempImage.Size)
                {
                    var shape = new ShapeHeader();

                    shape.MatrixFormat = (MatrixType)StreamUtil.ReadUInt8(stream);

                    if (shape.MatrixFormat != MatrixType.LongName)
                    {
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
                            if (shape.MatrixFormat == MatrixType.ColorPallet || shape.MatrixFormat == MatrixType.ColorPalletXbox)
                            {
                                RealSize = RealSize * 4;
                            }
                            if (shape.MatrixFormat == MatrixType.BGRA4444 || shape.MatrixFormat == MatrixType.BGR565)
                            {
                                RealSize = RealSize * 2;
                            }

                            shape.Matrix = StreamUtil.ReadBytes(stream, RealSize);
                        }
                        else
                        {
                            shape.Matrix = StreamUtil.ReadBytes(stream, shape.Size - 16);
                        }
                    }
                    else
                    {
                        stream.Position += 3;
                        tempImage.Longname = StreamUtil.ReadNullEndString(stream);
                        StreamUtil.AlignBy(stream, 16, tempImage.Offset);
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
                if (tempImage.MatrixType == MatrixType.FourBit || tempImage.MatrixType == MatrixType.EightBit
                    || tempImage.MatrixType == MatrixType.EightBitCompressed)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet);
                    tempImage.colorsTable = GetColorTable(tempImage);
                    tempImage = AlphaFix(tempImage);
                }

                if(tempImage.MatrixType == MatrixType.EightBitXbox)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPalletXbox);
                    tempImage.colorsTable = GetColorTable(tempImage);
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
                    case MatrixType.EightBitXbox:
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
                    case MatrixType.N64:
                        tempImage.Image = EADecode.DecodeMatrix30(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BC1:
                        tempImage.Image = EADecode.DecodeMatrix96(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BC2:
                        tempImage.Image = EADecode.DecodeMatrix97(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BGRA4444:
                        tempImage.Image = EADecode.DecodeMatrix109(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BGR565:
                        tempImage.Image = EADecode.DecodeMatrix120(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BGRA:
                        tempImage.Image = EADecode.DecodeMatrix125(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    default:
                        Console.WriteLine(tempImage.MatrixType + " Unknown Matrix");
                        break;
                }

                //Metal Bin
                //Need to add check

                ShapeImages[i] = tempImage;
            }
        }

        public void SaveShape(string path)
        {
            //Limit Colours for Saving
            for (int i = 0; i < ShapeImages.Count; i++)
            {
                var sshImage = ShapeImages[i];

                sshImage.colorsTable = ImageUtil.GetBitmapColorsFast(sshImage.Image).ToList();

                //if metal bin combine images and then reduce
                if (sshImage.colorsTable.Count > 16 && sshImage.MatrixType == MatrixType.FourBit)
                {
                    Console.WriteLine("Over 16 Colour Limit " + sshImage.Shortname + " (" + i + "/" + ShapeImages.Count + ")");
                    sshImage.Image = ImageUtil.ReduceBitmapColorsFast(sshImage.Image, 16);
                }
                if (sshImage.colorsTable.Count > 256 && sshImage.MatrixType == MatrixType.EightBit && sshImage.MatrixType == MatrixType.EightBitCompressed 
                    && sshImage.MatrixType == MatrixType.EightBitXbox)
                {
                    Console.WriteLine("Over 256 Colour Limit " + sshImage.Shortname + " (" + i + "/" + ShapeImages.Count + ")");
                    sshImage.Image = ImageUtil.ReduceBitmapColorsFast(sshImage.Image, 256);
                }
                ShapeImages[i] = sshImage;
            }

            //Write Header
            byte[] tempByte = new byte[4];
            Stream stream = new MemoryStream();

            StreamUtil.WriteString(stream, MagicWord, 4);

            long SizePos = stream.Position;
            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            StreamUtil.WriteInt32(stream, ShapeImages.Count, true);

            StreamUtil.WriteString(stream, Format, 4);

            List<int> intPos = new List<int>();

            for (int i = 0; i < ShapeImages.Count; i++)
            {
                intPos.Add((int)stream.Position);
                tempByte = new byte[4];
                stream.Write(tempByte, 0, tempByte.Length);
                StreamUtil.WriteString(stream, ShapeImages[i].Shortname, 4);
            }

            StreamUtil.WriteString(stream, EndingString, 8);

            StreamUtil.AlignBy16(stream);

            for (int i = 0; i < ShapeImages.Count; i++)
            {
                var TempMatrix = ImageWrite(ShapeImages[i]);

                StreamUtil.WriteBytes(stream, tempByte);

                StreamUtil.AlignBy16(stream);
            }
        }

        public byte[] ImageWrite(ShapeImage shapeImage)
        {
            Stream stream = new MemoryStream();

            shapeImage.Offset = (int)stream.Position;

            var Matrix = new byte[0];
            var Colours = new List<Rgba32>();

            if (shapeImage.MatrixType == MatrixType.FourBit)
            {
                var EncodedImage = EAEncode.EncodeMatrix1(shapeImage.Image);
                Matrix = EncodedImage.Matrix;
                Colours = EncodedImage.ColourTable;
                if (shapeImage.SwizzledImage)
                {
                    //Swizzle the Image
                    Matrix = ByteUtil.Swizzle4bpp(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                }
            }
            else if (shapeImage.MatrixType == MatrixType.EightBit)
            {
                //WriteMatrix2(stream, Image);
                var EncodedImage = EAEncode.EncodeMatrix2(shapeImage.Image);
                Matrix = EncodedImage.Matrix;
                Colours = EncodedImage.ColourTable;
                if (shapeImage.SwizzledImage)
                {
                    Matrix = ByteUtil.Swizzle8(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                }
            }
            else if (shapeImage.MatrixType == MatrixType.FullColor)
            {
                Matrix = EAEncode.EncodeMatrix5(shapeImage.Image);
                if (shapeImage.SwizzledImage)
                {
                    //Swizzle the Image
                }
            }
            else
            {
                Console.WriteLine(shapeImage.MatrixType + " Unknown Matrix");
            }

            //Compress Image
            if (shapeImage.MatrixType == MatrixType.EightBitCompressed)
            {
                //Compress Image
            }

            WriteImageHeader(stream, shapeImage, Matrix.Length);

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);

            if (shapeImage.MatrixType == MatrixType.FourBit || shapeImage.MatrixType == MatrixType.EightBit)
            {
                //Generate Colour Table Matrix
                WriteColourTable(stream, shapeImage);

                StreamUtil.AlignBy16(stream);
            }

            return StreamUtil.ReadBytes(stream, (int)stream.Length);
        }

        public void WriteImageHeader(Stream stream, ShapeImage image, int DataSize)
        {
            StreamUtil.WriteUInt8(stream, (int)image.MatrixType);

            StreamUtil.WriteInt24(stream, DataSize);

            StreamUtil.WriteInt16(stream, image.Image.Width);

            StreamUtil.WriteInt16(stream, image.Image.Height);

            StreamUtil.WriteInt16(stream, image.Xaxis);

            StreamUtil.WriteInt16(stream, image.Yaxis);

            int Flags = 0;
            Flags += (image.SwizzledImage ? 8192 : 0);

            StreamUtil.WriteInt32(stream, Flags);
        }

        public void WriteColourTable(Stream stream, ShapeImage image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(4 * image.colorsTable.Count, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int i = 0; i < image.colorsTable.Count; i++)
            {
                var Color = image.colorsTable[i];

                Matrix[i * 4] = Color.R;
                Matrix[i * 4 + 1] = Color.G;
                Matrix[i * 4 + 2] = Color.B;
                Matrix[i * 4 + 3] = Color.A;
                if (image.AlphaFix)
                {
                    Matrix[i * 4 + 3] = (byte)(Color.A / 2);
                }
            }

            WriteColourHeader(stream, image, Matrix.Length);

            if (image.SwizzledColours)
            {
                //Swizzle Colours
                Matrix = ByteUtil.SwizzlePalette(Matrix, image.colorsTable.Count);
            }

            StreamUtil.WriteBytes(stream, Matrix);
        }

        public void WriteColourHeader(Stream stream, ShapeImage image, int Size)
        {
            StreamUtil.WriteUInt8(stream, 33);
            int Flag1 = 1;
            int Flag2 = image.SwizzledColours ? 64 : 0;
            int Flag3 = 0;
            StreamUtil.WriteUInt8(stream, Flag1); // Probably not right
            StreamUtil.WriteUInt8(stream, Flag2);
            StreamUtil.WriteUInt8(stream, Flag3);

            StreamUtil.WriteInt32(stream, Size + 32);
            StreamUtil.WriteInt32(stream, 32);
            StreamUtil.WriteInt32(stream, Size);

            StreamUtil.WriteInt32(stream, 0);
            StreamUtil.WriteInt32(stream, 0);
            StreamUtil.WriteInt32(stream, image.colorsTable.Count);
            StreamUtil.WriteInt32(stream, 1);
        }

        public void AddImage(MatrixType matrixType, string name = "", string path = "")
        {
            var NewSSHImage = new ShapeImage();
            NewSSHImage.MatrixType = matrixType;
            NewSSHImage.Shortname = "????";
            NewSSHImage.Image = new Image<Rgba32>(1, 1);

            if(name!="")
            {
                NewSSHImage.Shortname = name;
            }

            if(path!="")
            {
                if(File.Exists(path))
                {
                    NewSSHImage.Image = (Image<Rgba32>)Image.Load(path);
                }
                else
                {
                    //Give error
                }
            }

            NewSSHImage.colorsTable = ImageUtil.GetBitmapColorsFast(NewSSHImage.Image).ToList();
            ShapeImages.Add(NewSSHImage);
        }

        private List<Rgba32> GetColorTable(ShapeImage newSSHImage)
        {
            var colorShape = GetShapeHeader(newSSHImage, MatrixType.ColorPallet);
            
            if(colorShape.MatrixFormat == MatrixType.Unknown)
            {
                colorShape = GetShapeHeader(newSSHImage, MatrixType.ColorPalletXbox);
            }


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
                    TempColour.A = (byte)A;
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
            ShapeImages[i] = temp;
        }

        //Neg 1 possibly not required
        //test
        public void BrightenImage(int i)
        {
            var TempImage = ShapeImages[i].Image;
            for (int y = 0; y < TempImage.Height; y++)
            {
                for (int x = 0; x < TempImage.Width; x++)
                {
                    Rgba32 color = TempImage[x, y];
                    color.R = (byte)(color.R * 2 - 1);
                    color.G = (byte)(color.G * 2 - 1);
                    color.B = (byte)(color.B * 2 - 1);

                    TempImage[x,y] = color;
                }
            }
            var tempimage = ShapeImages[i];
            tempimage.Image = TempImage;
            ShapeImages[i] = tempimage;
        }

        public void DarkenImage(int i)
        {
            var TempImage = ShapeImages[i].Image;
            for (int y = 0; y < TempImage.Height; y++)
            {
                for (int x = 0; x < TempImage.Width; x++)
                {
                    Rgba32 color = TempImage[x, y];
                    color.R = (byte)((color.R + 1 )/ 2);
                    color.G = (byte)((color.G + 1) / 2);
                    color.B = (byte)((color.B + 1) / 2);

                    TempImage[x, y] = color;
                }
            }
            var tempimage = ShapeImages[i];
            tempimage.Image = TempImage;
            ShapeImages[i] = tempimage;
        }

        public struct ShapeImage
        {
            public int Offset;
            public int Size;
            public string Shortname;
            public string Longname;
            public List<ShapeHeader> ShapeHeaders;

            //Converted
            public MatrixType MatrixType;
            public Image<Rgba32> Image;
            public Image<A8> Metal;
            public List<Rgba32> colorsTable;
            public int Xaxis;
            public int Yaxis;
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

            //PS2

            FourBit = 1,
            EightBit = 2,
            FullColor = 5,

            //N64
            N64 = 30,

            ColorPallet = 33,
            ColorPalletXbox = 42,

            //Xbox
            BC1 = 96,
            BC2 = 97,
            BGRA4444 = 109,
            BGR565 = 120,
            EightBitXbox = 123,
            BGRA = 125,

            //Other
            MetalAlpha = 105,
            LongName = 112,

            EightBitCompressed = 130,
        }
    }
}
