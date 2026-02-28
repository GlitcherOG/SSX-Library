using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.FileHandlers;
using SSX_Library.Internal.Utilities;
using System.Text;
using SSX_Library.Internal;

namespace SSX_Library.EATextureLibrary
{
    public class NewShapeHandler
    {
        public string MagicWord; //4
        private int Size;
        private int ImageCount; //Big 4
        private int U0;
        public string Group;
        public string EndingString;

        public List<ShapeImage> ShapeImages = new List<ShapeImage>();

        public void LoadShape(string path)
        {
            ShapeImages = new List<ShapeImage>();
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

                        ShapeImages.Add(tempImage);
                    }

                    Group = StreamUtil.ReadString(stream, 8);

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
                    shape.Flags1 = StreamUtil.ReadUInt8(stream); //Bit Flags? +1 - Image?, +2 - Compressed,  
                    shape.Flags2 = StreamUtil.ReadUInt8(stream); //Flags? +64 - Swizzled,
                    shape.Flags3 = StreamUtil.ReadUInt8(stream);
                    shape.Size = StreamUtil.ReadUInt32(stream);
                    shape.U2 = StreamUtil.ReadUInt32(stream);
                    shape.DataSize = StreamUtil.ReadUInt32(stream);
                    if (shape.MatrixFormat != MatrixType.LongName)
                    {
                        shape.U4 = StreamUtil.ReadUInt32(stream);
                        shape.U5 = StreamUtil.ReadUInt32(stream);
                        shape.XSize = StreamUtil.ReadUInt32(stream);
                        shape.YSize = StreamUtil.ReadUInt32(stream);
                    }

                    if (shape.MatrixFormat == MatrixType.LongName)
                    {
                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.U2);
                    }
                    else
                    if (shape.Size == 0)
                    {
                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.DataSize);
                    }
                    else
                    {
                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.Size - 32);
                    }

                    //StreamUtil.AlignBy16(stream);

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
                    imageMatrix.Matrix = Refpack.Decompress(imageMatrix.Matrix);
                }

                //Process Colors
                //Todo Check If Type is here instead
                if (tempImage.MatrixType == MatrixType.FourBit || tempImage.MatrixType == MatrixType.EightBit)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet);
                    tempImage.SwizzledColours = (colorShape.Flags2 & 64) == 64;
                    tempImage.colorsTable = GetColorTable(tempImage);
                    tempImage = AlphaFix(tempImage);
                }


                //Process into image
                switch (tempImage.MatrixType)
                {
                    case MatrixType.FourBit:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle4bpp(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        }
                        tempImage.Image = EADecode.DecodeMatrix1(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                        break;
                    case MatrixType.EightBit:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.Unswizzle8(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        }
                        tempImage.Image = EADecode.DecodeMatrix2(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                        break;
                    case MatrixType.FullColor:
                        tempImage.Image = EADecode.DecodeMatrix5(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    default:
                        Console.WriteLine(tempImage.MatrixType + " Unknown Matrix");
                        break;
                }

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
            var colorShape = GetShapeHeader(newSSHImage, MatrixType.ColorPallet);
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
                if (newSSHImage.ShapeHeaders[i].MatrixFormat==Type)
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
                    tempImage.ShapeHeaders[i].MatrixFormat == MatrixType.FullColor)
                {
                    return (MatrixType)tempImage.ShapeHeaders[i].MatrixFormat;
                }
            }

            return (MatrixType)tempImage.ShapeHeaders[0].MatrixFormat;
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
                if (sshImage.colorsTable.Count > 256 && sshImage.MatrixType == MatrixType.EightBit)
                {
                    Console.WriteLine("Over 256 Colour Limit " + sshImage.Shortname + " (" + i + "/" + ShapeImages.Count + ")");
                    sshImage.Image = ImageUtil.ReduceBitmapColorsFast(sshImage.Image, 256);
                }
                ShapeImages[i] = sshImage;
            }

            //Write Header
            byte[] tempByte = new byte[4];
            Stream stream = new MemoryStream();

            StreamUtil.WriteString(stream, MagicWord,4);

            long SizePos = stream.Position;
            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            StreamUtil.WriteInt32(stream, ShapeImages.Count, true);

            StreamUtil.WriteInt32(stream, U0);

            List<int> intPos = new List<int>();

            for (int i = 0; i < ShapeImages.Count; i++)
            {
                intPos.Add((int)stream.Position);
                tempByte = new byte[8];
                stream.Write(tempByte, 0, tempByte.Length);
                StreamUtil.WriteNullString(stream, ShapeImages[i].Shortname);
            }

            StreamUtil.WriteString(stream, Group, 8);

            StreamUtil.WriteString(stream, EndingString, 8);

            StreamUtil.AlignBy16(stream);

            //Process Image to SSHShapeHeader
            for (int i = 0; i < ShapeImages.Count; i++)
            {
                var Image = ShapeImages[i];

                Image.Offset = (int)stream.Position;

                var Matrix = new byte[0];
                var Colours = new List<Rgba32>();

                if (Image.MatrixType == MatrixType.FourBit)
                {
                    var EncodedImage = EAEncode.EncodeMatrix1(Image.Image);
                    Matrix = EncodedImage.Matrix;
                    Colours = EncodedImage.ColourTable;
                    if (Image.SwizzledImage)
                    {
                        //Swizzle the Image
                        Matrix = ByteUtil.Swizzle4bpp(Matrix, Image.Image.Width, Image.Image.Height);
                    }
                }
                else if (Image.MatrixType == MatrixType.EightBit)
                {
                    //WriteMatrix2(stream, Image);
                    var EncodedImage = EAEncode.EncodeMatrix2(Image.Image);
                    Matrix = EncodedImage.Matrix;
                    Colours = EncodedImage.ColourTable;
                    if (Image.SwizzledImage)
                    {
                        Matrix = ByteUtil.Swizzle8(Matrix, Image.Image.Width, Image.Image.Height);
                    }
                }
                else if (Image.MatrixType == MatrixType.FullColor)
                {
                    Matrix = EAEncode.EncodeMatrix5(Image.Image);
                    if (Image.SwizzledImage)
                    {
                        //Swizzle the Image
                    }
                }
                else
                {
                    Console.WriteLine(Image.MatrixType + " Unknown Matrix");
                    return;
                }

                //Compress Image
                if (Image.Compressed)
                {
                    //Compress Image
                }

                WriteImageHeader(stream, Image, Matrix.Length);

                StreamUtil.WriteBytes(stream, Matrix);

                //Might not be needed
                StreamUtil.AlignBy16(stream);

                if (Image.MatrixType == MatrixType.FourBit || Image.MatrixType == MatrixType.EightBit)
                {
                    //Generate Colour Table Matrix
                    WriteColourTable(stream, Image);

                    StreamUtil.AlignBy16(stream);
                }

                //Write Long Name
                //FIX SOON

                Image.Size = (int)stream.Position - Image.Offset;

                ShapeImages[i] = Image;
            }

            //Go back and write headers idiot
            int Size = (int)stream.Position;

            stream.Position = SizePos;
            StreamUtil.WriteInt32(stream, Size);

            for (int i = 0; i < intPos.Count; i++)
            {
                stream.Position = intPos[i];

                StreamUtil.WriteInt32(stream, ShapeImages[i].Offset, true);
                StreamUtil.WriteInt32(stream, ShapeImages[i].Size, true);
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

        public void WriteImageHeader(Stream stream, ShapeImage image, int DataSize)
        {
            StreamUtil.WriteUInt8(stream, (int)image.MatrixType);
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
            internal int Offset;
            internal int Size;
            public string Shortname;
            public string Longname;
            internal List<ShapeHeader> ShapeHeaders;

            //Converted
            public List<Rgba32> colorsTable;
            public Image<Rgba32> Image;
            public MatrixType MatrixType;
            public bool Compressed;
            public bool SwizzledImage;
            public bool SwizzledColours;
            public bool AlphaFix;
        }

        public struct ShapeHeader
        {
            public MatrixType MatrixFormat;
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

        public enum MatrixType : byte
        {
            Unknown = 0,
            FourBit = 1,
            EightBit = 2,
            FullColor = 5,

            ColorPallet = 33,
            LongName = 111,
        }
    }
}
