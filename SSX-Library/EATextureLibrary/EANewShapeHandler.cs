using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.FileHandlers;
using SSXLibrary.Utilities;

namespace SSX_Library.EATextureLibrary
{
    public class EANewShapeHandler
    {
        public string MagicWord; //4
        public int Size;
        public int ImageCount; //Big 4
        public int U0;
        public List<ShapeImage> sshImages = new List<ShapeImage>();
        public string group;
        public string endingstring;

        public void LoadShape(string path)
        {
            sshImages = new List<ShapeImage>();
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MagicWord = StreamUtil.ReadString(stream, 4);

                if (MagicWord == "ShpS")
                {
                    Size = StreamUtil.ReadUInt32(stream);

                    ImageCount = StreamUtil.ReadUInt32(stream, true);

                    U0 = StreamUtil.ReadUInt32(stream, true);

                    for (int i = 0; i < ImageCount; i++)
                    {
                        ShapeImage tempImage = new ShapeImage();

                        tempImage.Offset = StreamUtil.ReadUInt32(stream, true);

                        tempImage.Size = StreamUtil.ReadUInt32(stream, true);

                        tempImage.Shortname = StreamUtil.ReadNullEndString(stream);

                        stream.Position++;

                        sshImages.Add(tempImage);
                    }

                    group = StreamUtil.ReadString(stream, 8);

                    endingstring = StreamUtil.ReadString(stream, 8);

                    ProcessImages(stream);
                }
                else
                {
                    //MessageBox.Show(MagicWord + " Unsupported format");
                }
                stream.Dispose();
                stream.Close();
            }
        }

        private void ProcessImages(Stream stream)
        {
            for (int i = 0; i < sshImages.Count; i++)
            {
                ShapeImage tempImage = sshImages[i];
                stream.Position = tempImage.Offset;

                tempImage.ShapeHeaders = new List<ShapeHeader>();

                while (stream.Position < tempImage.Offset + tempImage.Size)
                {
                    var shape = new ShapeHeader();

                    shape.MatrixFormat = StreamUtil.ReadUInt8(stream);
                    if (shape.MatrixFormat != 111)
                    {
                        shape.Flags1 = StreamUtil.ReadUInt8(stream); //Bit Flags? +1 - Image?, +2 - Compressed,  
                        shape.Flags2 = StreamUtil.ReadUInt8(stream); //Flags? +64 - Swizzled,
                        shape.Flags3 = StreamUtil.ReadUInt8(stream);
                        shape.Size = StreamUtil.ReadUInt32(stream);
                        shape.U2 = StreamUtil.ReadUInt32(stream);
                        shape.DataSize = StreamUtil.ReadUInt32(stream);
                        shape.U4 = StreamUtil.ReadUInt32(stream);
                        shape.U5 = StreamUtil.ReadUInt32(stream);
                        shape.XSize = StreamUtil.ReadUInt32(stream);
                        shape.YSize = StreamUtil.ReadUInt32(stream);

                        if (shape.Size == 0)
                        {
                            shape.Matrix = StreamUtil.ReadBytes(stream, shape.DataSize);
                        }
                        else
                        {
                            shape.Matrix = StreamUtil.ReadBytes(stream, shape.Size - 32);
                        }
                    }
                    else
                    {
                        shape.Flags1 = StreamUtil.ReadUInt8(stream); //Bit Flags? +1 - Image?, +2 - Compressed,  
                        shape.Flags2 = StreamUtil.ReadUInt8(stream); //Flags? +64 - Swizzled,
                        shape.Flags3 = StreamUtil.ReadUInt8(stream);
                        shape.Size = StreamUtil.ReadUInt32(stream);
                        shape.U2 = StreamUtil.ReadUInt32(stream);
                        shape.DataSize = StreamUtil.ReadUInt32(stream);

                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.DataSize);
                    }
                    tempImage.ShapeHeaders.Add(shape);
                }

                //Get Matrix Type
                tempImage.MatrixType  = GetShapeMatrixType(tempImage);
                var imageMatrix = GetShapeHeader(tempImage, tempImage.MatrixType);

                tempImage.Compressed = (imageMatrix.Flags1 & 2) == 2;
                tempImage.SwizzledImage = (imageMatrix.Flags2 & 64) == 64;

                //Uncompress
                if (tempImage.Compressed)
                {
                    imageMatrix.Matrix = RefpackHandler.Decompress(imageMatrix.Matrix);
                }

                //Process Colors
                //Todo Check If Type is here instead
                if (tempImage.MatrixType == 1 || tempImage.MatrixType == 2)
                {
                    var colorShape = GetShapeHeader(tempImage, 33);
                    tempImage.SwizzledColours = (colorShape.Flags2 & 64) == 64;
                    tempImage.colorsTable = GetColorTable(tempImage);
                    tempImage = AlphaFix(tempImage);
                }


                //Process into image
                switch (tempImage.MatrixType)
                {
                    case 1:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle4bpp(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        }
                        tempImage.Image = EADecode.DecodeMatrix1(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                        break;
                    case 2:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle8(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        }
                        tempImage.Image = EADecode.DecodeMatrix2(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                        break;
                    case 3:
                        tempImage.Image = EADecode.DecodeMatrix5(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    default:
                        Console.WriteLine(tempImage.MatrixType + " Unknown Matrix");
                        break;
                }

                sshImages[i] = tempImage;
            }
        }

        private List<Rgba32> GetColorTable(ShapeImage newSSHImage)
        {
            var colorShape = GetShapeHeader(newSSHImage, 33);
            List<Rgba32> colors = new List<Rgba32>();

            if (newSSHImage.SwizzledColours)
            {
                colorShape.Matrix = ByteUtil.UnswizzlePalette(colorShape.Matrix, colorShape.XSize);
            }

            for (int i = 0; i < colorShape.XSize * colorShape.YSize; i++)
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
                if (newSSHImage.colorsTable[i].A>0x80)
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

        private ShapeHeader GetShapeHeader(ShapeImage newSSHImage, int Type)
        {
            for (int i = 0; i < newSSHImage.ShapeHeaders.Count; i++)
            {
                if (newSSHImage.ShapeHeaders[i].MatrixFormat==Type)
                {
                    return newSSHImage.ShapeHeaders[i];
                }
            }
            return new ShapeHeader();
        }

        private int GetShapeMatrixType(int ImageID)
        {
            return GetShapeMatrixType(sshImages[ImageID]);
        }

        private int GetShapeMatrixType(ShapeImage tempImage)
        {
            for (int i = 0; i < tempImage.ShapeHeaders.Count; i++)
            {
                if (tempImage.ShapeHeaders[i].MatrixFormat == 1 || tempImage.ShapeHeaders[i].MatrixFormat == 2 || tempImage.ShapeHeaders[i].MatrixFormat == 5)
                {
                    return tempImage.ShapeHeaders[i].MatrixFormat;
                }
            }

            return tempImage.ShapeHeaders[0].MatrixFormat;
        }

        public void ExtractImage(string path)
        {
            for (int i = 0; i < sshImages.Count; i++)
            {
                sshImages[i].Image.SaveAsPng(System.IO.Path.Combine(path, sshImages[i].Shortname + i + ".png"));
            }
        }

        public void ExtractSingleImage(string path, int i)
        {
            sshImages[i].Image.SaveAsPng(path);
        }

        public void LoadSingleImage(string path, int i)
        {
            var temp = sshImages[i];
            temp.Image = (Image<Rgba32>)Image.Load(path);
            temp.colorsTable = ImageUtil.GetBitmapColorsFast(temp.Image).ToList();
            temp.MatrixType = sshImages[i].MatrixType;
            sshImages[i] = temp;
        }

        public void SaveShape(string path, bool TestImages = true)
        {
            for (int i = 0; i < sshImages.Count; i++)
            {
                if (TestImages)
                {
                    var sshImage = sshImages[i];

                    sshImage.colorsTable = ImageUtil.GetBitmapColorsFast(sshImage.Image).ToList();

                    //if metal bin combine images and then reduce

                    if (sshImage.colorsTable.Count > 256 && sshImage.MatrixType == 2)
                    {
                        Console.WriteLine("Over 256 Colour Limit " + sshImage.Shortname + " (" + i + "/" + sshImages.Count + ")");
                        sshImage.Image = ImageUtil.ReduceBitmapColorsFast(sshImage.Image, 256);
                        //MessageBox.Show(sshImages[i].shortname + " " + i.ToString() + " Exceeds 256 Colours");
                        //check = true;
                    }
                    if (sshImage.colorsTable.Count > 16 && sshImage.MatrixType == 1)
                    {
                        Console.WriteLine("Over 16 Colour Limit " + sshImage.Shortname + " (" + i + "/" + sshImages.Count + ")");
                        sshImage.Image = ImageUtil.ReduceBitmapColorsFast(sshImage.Image, 16);
                        //MessageBox.Show(sshImage.shortname + " " + i.ToString() + " Exceeds 16 Colours");
                        //check = true;
                    }
                    sshImages[i] = sshImage;
                }
            }

            //Write Header

            byte[] tempByte = new byte[4];
            Stream stream = new MemoryStream();

            StreamUtil.WriteString(stream, MagicWord,4);

            long SizePos = stream.Position;
            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            StreamUtil.WriteInt32(stream, sshImages.Count, true);

            StreamUtil.WriteInt32(stream, U0);

            List<int> intPos = new List<int>();

            for (int i = 0; i < sshImages.Count; i++)
            {
                intPos.Add((int)stream.Position);
                tempByte = new byte[8];
                stream.Write(tempByte, 0, tempByte.Length);
                StreamUtil.WriteNullString(stream, sshImages[i].Shortname);
            }

            StreamUtil.WriteString(stream, group, 8);

            StreamUtil.WriteString(stream, "Buy ERTS", 8);

            StreamUtil.AlignBy16(stream);

            //Process Image to SSHShapeHeader
            for (int i = 0; i < sshImages.Count; i++)
            {
                var Image = sshImages[i];

                Image.Offset = (int)stream.Position;

                if(Image.MatrixType==1)
                {
                    WriteMatrix1(stream, Image);
                }
                else if (Image.MatrixType == 2)
                {
                    WriteMatrix2(stream, Image);
                }
                else if (Image.MatrixType == 5)
                {
                    WriteMatrix5(stream, Image);
                }
                else
                {
                    Console.WriteLine(Image.MatrixType + " Unknown Matrix");
                    return;
                }

                //Write Long Name



                Image.Size = (int)stream.Position - Image.Offset;

                sshImages[i] = Image;
            }

            //Go back and write headers idiot
            int Size = (int)stream.Position;

            stream.Position = SizePos;
            StreamUtil.WriteInt32(stream, Size);

            for (int i = 0; i < intPos.Count; i++)
            {
                stream.Position = intPos[i];

                StreamUtil.WriteInt32(stream, sshImages[i].Offset, true);
                StreamUtil.WriteInt32(stream, sshImages[i].Size, true);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var file = File.Create(path);
            stream.Position = 0;
            stream.CopyTo(file);
            stream.Dispose();
            file.Close();
        }

        public void WriteMatrix1(Stream stream, ShapeImage image)
        {
            byte[] TempMatrix = new byte[image.Image.Height * image.Image.Width];

            for (int y = 0; y < image.Image.Height; y++)
            {
                for (int x = 0; x < image.Image.Width; x++)
                {
                    TempMatrix[y * image.Image.Width + x] = (byte)image.colorsTable.IndexOf(image.Image[x,y]);
                }
            }

            int MatrixSize = StreamUtil.AlignbyMath(TempMatrix.Length / 2, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int i = 0; i < TempMatrix.Length / 2; i++)
            {
                Matrix[i] = (byte)ByteUtil.BitConbineConvert(TempMatrix[i*2], TempMatrix[i*2+1], 0, 4, 4);
            }


            if (image.SwizzledImage)
            {
                //Swizzle the Image
                Matrix = ByteUtil.Swizzle4bpp(Matrix, image.Image.Width, image.Image.Height);
            }

            if (image.Compressed)
            {
                //Compress Image
            }

            WriteImageHeader(stream, image, Matrix.Length);

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);

            //Generate Colour Table Matrix
            WriteColourTable(stream, image);

            StreamUtil.AlignBy16(stream);
        }
        public void WriteMatrix2(Stream stream, ShapeImage image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(image.Image.Height * image.Image.Width, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int y = 0; y < image.Image.Height; y++)
            {
                for (int x = 0; x < image.Image.Width; x++)
                {
                    Matrix[y* image.Image.Width + x] = (byte)image.colorsTable.IndexOf(image.Image[x, y]);
                }
            }

            if(image.SwizzledImage)
            {
                Matrix = ByteUtil.Swizzle8(Matrix, image.Image.Width, image.Image.Height);
            }

            if(image.Compressed)
            {
                //Compress Image
            }

            WriteImageHeader(stream, image, Matrix.Length);

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);

            //Generate Colour Table Matrix
            WriteColourTable(stream, image);
        }
        public void WriteMatrix5(Stream stream, ShapeImage image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(image.Image.Height * image.Image.Width * 4, 16);

            MatrixSize = 16 - MatrixSize % 16;

            byte[] Matrix = new byte[MatrixSize];

            for (int y = 0; y < image.Image.Height; y++)
            {
                for (int x = 0; x < image.Image.Width; x++)
                {
                    var Pixel = image.Image[x, y];
                    Matrix[y * x + x * 4] = Pixel.R;
                    Matrix[y * x + x * 4+1] = Pixel.G;
                    Matrix[y * x + x * 4+2] = Pixel.B;
                    Matrix[y * x + x * 4+3] = Pixel.A;
                }
            }

            if (image.SwizzledImage)
            {
                //Swizzle the Image
            }

            if (image.Compressed)
            {
                //Compress Image
            }

            WriteImageHeader(stream, image, Matrix.Length);

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);
        }

        public void WriteImageHeader(Stream stream, ShapeImage image, int DataSize)
        {
            StreamUtil.WriteUInt8(stream, image.MatrixType);
            int Flag1 = 1 + (image.Compressed ? 2 : 0);
            int Flag2 = image.SwizzledImage ? 64 : 0;
            int Flag3 = 0;
            StreamUtil.WriteUInt8(stream, Flag1);
            StreamUtil.WriteUInt8(stream, Flag2);
            StreamUtil.WriteUInt8(stream, Flag3);

            StreamUtil.WriteInt32(stream, DataSize + 32);
            StreamUtil.WriteInt32(stream, 0);
            StreamUtil.WriteInt32(stream, 0);

            StreamUtil.WriteInt32(stream, DataSize);
            StreamUtil.WriteInt32(stream, 0);
            StreamUtil.WriteInt32(stream, image.Image.Width);
            StreamUtil.WriteInt32(stream, image.Image.Height);
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

            if(image.SwizzledColours)
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
            public int Unknown;
            public int MatrixType;
            public bool Compressed;
            public bool SwizzledImage;
            public bool SwizzledColours;
            public bool AlphaFix;
        }

        public struct ShapeHeader
        {
            public byte MatrixFormat;
            public int Flags1;
            public int Flags2;
            public int Flags3;
            public int Size;
            public int U2;
            public int DataSize;

            public int U4;
            public int U5;
            public int XSize;
            public int YSize;

            public byte[] Matrix;
        }
    }
}
