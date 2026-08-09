using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System.Text;


namespace SSX_Library.EATextureLibrary
{
    public class OldShapeHandler
    {
        //Ensure cant set bad version
        public TextureType ConsoleVersion = TextureType.OldPS2;
        private string MagicWord;
        private int FileSize;
        private int ImageCount;
        public string Format;
        public string EndingString;
        public List<ShapeImage> ShapeImages = new List<ShapeImage>();

        public void LoadShape(string path)
        {
            ShapeImages = new List<ShapeImage>();
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MagicWord = StreamUtil.ReadString(stream, 4);

                var Type = OldSignatureCheck(MagicWord);

                if (OldSignatureCheck(MagicWord)!=null)
                {
                    ConsoleVersion = Type.Value;

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

                    for (int i = 0; i < ShapeImages.Count; i++)
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

        public TextureType? OldSignatureCheck(string signature)
        {
            switch (signature)
            {
                case "SHPS": //PS2
                    return TextureType.OldPS2;
                case "SHPX": //Xbox
                    return TextureType.OldXbox;
                case "SHPG": //GameCube
                    return TextureType.OldGC;
                case "SHPM": //PSP
                    return TextureType.OldPSP;
                default:
                    return null;
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

                    string TestEnd = StreamUtil.ReadString(stream, 8);
                    stream.Position -= 8;
                    if(TestEnd == EndingString)
                    {
                        break;
                    }

                    shape.MatrixFormat = (MatrixType)StreamUtil.ReadUInt8(stream);

                    if (shape.MatrixFormat != MatrixType.LongName && shape.MatrixFormat != MatrixType.Unknown1 && shape.MatrixFormat != MatrixType.Unknown)
                    {
                        shape.Size = StreamUtil.ReadUInt24(stream);

                        shape.Width = StreamUtil.ReadInt16(stream);

                        shape.Height = StreamUtil.ReadInt16(stream);

                        shape.Xaxis = StreamUtil.ReadInt16(stream);

                        shape.Yaxis = StreamUtil.ReadInt16(stream);

                        //Add Other Flags Later
                        shape.Flags = StreamUtil.ReadInt32(stream);

                        if (shape.Size == 0 || shape.MatrixFormat == MatrixType.LongName)
                        {
                            int RealSize = shape.Width * shape.Height;
                            if (shape.MatrixFormat == MatrixType.ColorPallet || shape.MatrixFormat == MatrixType.ColorPallet_Xbox || shape.MatrixFormat == MatrixType.FullColor)
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
                    else if (shape.MatrixFormat == MatrixType.LongName)
                    {
                        stream.Position += 3;
                        tempImage.Longname = StreamUtil.ReadNullEndString(stream);
                        StreamUtil.AlignBy(stream, 16, tempImage.Offset);
                    }
                    else if (shape.MatrixFormat == MatrixType.Unknown1)
                    {
                        shape.Size = StreamUtil.ReadUInt24(stream);

                        shape.Width = StreamUtil.ReadInt16(stream);

                        shape.Matrix = StreamUtil.ReadBytes(stream, shape.Width*8);

                        StreamUtil.AlignBy16(stream);
                    }
                    else
                    {
                        throw new Exception("Unknown Shape");
                    }

                    tempImage.ShapeHeaders.Add(shape);
                }

                //Get Matrix Type
                tempImage.MatrixType = GetShapeMatrixType(tempImage);
                var imageMatrix = GetShapeHeader(tempImage, tempImage.MatrixType).Value;

                tempImage.SwizzledImage = (imageMatrix.Flags & 8192) == 8192;

                //Uncompress
                if (imageMatrix.Matrix != null && imageMatrix.Matrix.Length > 0 && imageMatrix.MatrixFormat == MatrixType.EightBitCompressed) 
                {
                    imageMatrix.Matrix = Refpack.Decompress(imageMatrix.Matrix);
                }

                //Metal Check
                var MetalCheck = GetShapeHeader(tempImage, MatrixType.MetalAlpha);

                //Process Colors
                //Todo Check If Type is here instead
                if ((tempImage.MatrixType == MatrixType.FourBit || tempImage.MatrixType == MatrixType.EightBit
                    || tempImage.MatrixType == MatrixType.EightBitCompressed))
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet);
                    tempImage.SwizzledColours = (colorShape.Value.Flags & 8192) == 8192;
                    tempImage.colorsTable = GetColorTable(tempImage, MatrixType.ColorPallet);
                    if (MetalCheck == null)
                    {
                        tempImage = AlphaFix(tempImage);
                    }
                }

                if(tempImage.MatrixType == MatrixType.EightBitXbox)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet_Xbox);
                    tempImage.colorsTable = GetColorTable(tempImage, MatrixType.ColorPallet_Xbox);
                }

                if (tempImage.MatrixType == MatrixType.EightBit_PSP)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet_PSP);
                    tempImage.colorsTable = GetColorTablePSP(tempImage, MatrixType.ColorPallet_PSP);
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
                    case MatrixType.EightBit_PSP:
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
                    case MatrixType.BC1_PSP:
                    case MatrixType.BC1:
                        tempImage.Image = EADecode.DecodeMatrixDXT1(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
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

                //Metal Alpha
                if (MetalCheck!=null)
                {
                    var TempTexture = tempImage.Image;
                    var NewImage = new Image<A8>(TempTexture.Width, TempTexture.Height);

                    for (int Y = 0; Y < TempTexture.Height; Y++)
                    {
                        for (int X = 0; X < TempTexture.Width; X++)
                        {
                            NewImage[X, Y] = new A8(TempTexture[X, Y].A);
                            TempTexture[X, Y] = new Rgba32(TempTexture[X, Y].R, TempTexture[X, Y].G, TempTexture[X, Y].B);
                        }
                    }

                    tempImage.Image = TempTexture;
                    tempImage.MetalAlpha = true;
                    tempImage.Metal = NewImage;
                }

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
                if (sshImage.colorsTable.Count > 256 && sshImage.MatrixType == MatrixType.EightBit)
                {
                    Console.WriteLine("Over 256 Colour Limit " + sshImage.Shortname + " (" + i + "/" + ShapeImages.Count + ")");
                    // EightBit stores one byte per texel, so the palette must fit in 256 entries.
                    // Quantize with alpha awareness; ReduceBitmapColorsFast is RGB-only and drops transparency.
                    sshImage.Image.Mutate(x => x.Quantize(new WuQuantizer(new QuantizerOptions { MaxColors = 256 })));
                    sshImage.colorsTable = ImageUtil.GetBitmapColorsFast(sshImage.Image).ToList();
                }
                ShapeImages[i] = sshImage;
            }

            //Pick Magic
            switch (ConsoleVersion)
            {
                case TextureType.OldPS2: //PS2
                    MagicWord = "SHPS";
                    break;
                case TextureType.OldXbox: //Xbox
                    MagicWord = "SHPX";
                    break;
                case TextureType.OldGC: //GameCube
                    MagicWord = "SHPG";
                    break;
            }

            //Write Header
            byte[] tempByte = new byte[4];
            Stream stream = new MemoryStream();

            StreamUtil.WriteString(stream, MagicWord, 4);

            long SizePos = stream.Position;
            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            StreamUtil.WriteInt32(stream, ShapeImages.Count);

            StreamUtil.WriteString(stream, Format, 4);

            List<int> intPos = new List<int>();

            for (int i = 0; i < ShapeImages.Count; i++)
            {
                StreamUtil.WriteString(stream, ShapeImages[i].Shortname, 4);
                intPos.Add((int)stream.Position);
                tempByte = new byte[4];
                stream.Write(tempByte, 0, tempByte.Length);
            }

            StreamUtil.WriteString(stream, "Buy ERTS", 8);

            StreamUtil.AlignBy16(stream);

            for (int i = 0; i < ShapeImages.Count; i++)
            {
                int TempPos = (int)stream.Position;
                stream.Position = intPos[i];
                StreamUtil.WriteInt32(stream, TempPos);
                stream.Position = TempPos;

                var TempMatrix = ImageWrite(ShapeImages[i]);

                StreamUtil.WriteBytes(stream, TempMatrix);

                StreamUtil.AlignBy16(stream);
                StreamUtil.WriteString(stream, "Buy ERTS", 8);
                StreamUtil.AlignBy16(stream);
            }

            //Go back and write headers idiot
            int Size = (int)stream.Position;

            stream.Position = SizePos;
            StreamUtil.WriteInt32(stream, Size);

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

        public byte[] ImageWrite(ShapeImage shapeImage)
        {
            Stream stream = new MemoryStream();

            shapeImage.Offset = (int)stream.Position;

            //If Metal Alpha combine textures
            if(shapeImage.MetalAlpha)
            {
                var NewImage = new Image<Rgba32>(shapeImage.Image.Height, shapeImage.Image.Width);

                for (int Y = 0; Y < NewImage.Height; Y++)
                {
                    for (int X = 0; X < NewImage.Width; X++)
                    {
                        NewImage[X, Y] = new Rgba32(shapeImage.Image[X, Y].R, shapeImage.Image[X, Y].G, shapeImage.Image[X, Y].B, shapeImage.Metal[X, Y].PackedValue);
                    }
                }
                shapeImage.AlphaFix = false;

                shapeImage.Image = NewImage;
            }

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
            else if (shapeImage.MatrixType == MatrixType.EightBit || shapeImage.MatrixType == MatrixType.EightBitXbox || shapeImage.MatrixType == MatrixType.EightBitCompressed)
            {
                var EncodedImage = EAEncode.EncodeMatrix2(shapeImage.Image);
                Matrix = EncodedImage.Matrix;
                Colours = EncodedImage.ColourTable;
                // EncodeMatrix2 assigns texel indices in first-seen order, so the written palette must be
                // its colour table, not the one prepared by SaveShape. Retail 8-bit shapes always carry a
                // full 256-entry palette, so pad short tables to keep that layout.
                shapeImage.colorsTable = Colours;
                if (shapeImage.MatrixType == MatrixType.EightBit)
                {
                    while (shapeImage.colorsTable.Count < 256)
                    {
                        shapeImage.colorsTable.Add(new Rgba32());
                    }
                }
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
                byte[] TempBytes = Refpack.Compress(Matrix);
                Matrix = TempBytes;
            }

            WriteImageHeader(stream, shapeImage, Matrix.Length+16);

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);

            if (shapeImage.MatrixType == MatrixType.FourBit || shapeImage.MatrixType == MatrixType.EightBit)
            {
                //Generate Colour Table Matrix
                WriteColourTable(stream, shapeImage);

                StreamUtil.AlignBy16(stream);
            }

            //Write Metal Alpha
            if (shapeImage.MetalAlpha)
            {
                stream.WriteByte((byte)MatrixType.MetalAlpha);
                stream.WriteUInt24(16, ByteOrder.LittleEndian);
                stream.WriteUInt16(0, ByteOrder.LittleEndian);
                stream.WriteUInt16(128, ByteOrder.LittleEndian);
                stream.AlignBy16();
            }

            //Write Longname
            if (shapeImage.Longname != "")
            {
                stream.WriteUInt32((byte)MatrixType.LongName, ByteOrder.LittleEndian);
                stream.WriteAsciiWithLength(shapeImage.Longname, 12);
            }

            stream.Position = 0;
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
                    // Halve into GS range, keeping the opaque endpoint exact (255 -> 128) like DarkenImage.
                    Matrix[i * 4 + 3] = (byte)((Color.A + 1) / 2);
                }
            }

            if (image.SwizzledColours)
            {
                //Swizzle Colours
                Matrix = ByteUtil.SwizzlePalette(Matrix, image.colorsTable.Count);
                //Limit Matrix to Remove Bloat
            }

            WriteColourHeader(stream, image, Matrix.Length+16);

            StreamUtil.WriteBytes(stream, Matrix);
        }

        public void WriteColourHeader(Stream stream, ShapeImage image, int Size)
        {
            StreamUtil.WriteUInt8(stream, 33);

            StreamUtil.WriteInt24(stream, Size);

            StreamUtil.WriteInt16(stream, image.colorsTable.Count);

            StreamUtil.WriteInt16(stream, 1);

            StreamUtil.WriteInt16(stream, image.colorsTable.Count);

            StreamUtil.WriteInt16(stream, 0);

            int Flags = 0;
            Flags += (image.SwizzledColours ? 8192 : 0);

            StreamUtil.WriteInt32(stream, Flags);
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
                    // Decode into the representation ShapeImage actually stores. An untyped load preserves
                    // an opaque PNG as Image<Rgb24>, which cannot be cast to Image<Rgba32> even though the
                    // pixels are losslessly convertible (and made repack reject ordinary RGB terrain art).
                    NewSSHImage.Image = Image.Load<Rgba32>(path);
                }
                else
                {
                    //Give error
                    throw new Exception("No File at Path " + path); 
                }
            }

            NewSSHImage.colorsTable = ImageUtil.GetBitmapColorsFast(NewSSHImage.Image).ToList();
            ShapeImages.Add(NewSSHImage);
        }

        private List<Rgba32> GetColorTable(ShapeImage newSSHImage, MatrixType matrixType)
        {
            var colorShape = GetShapeHeader(newSSHImage, matrixType).Value;
            
            //if(colorShape.MatrixFormat == MatrixType.Unknown)
            //{
            //    colorShape = GetShapeHeader(newSSHImage, MatrixType.ColorPallet_Xbox).Value;
            //}
            int RealColour = colorShape.Width * colorShape.Height;

            if (colorShape.Size != 0)
            {
                RealColour = (colorShape.Size - 16) / 4;
            }

            if (newSSHImage.SwizzledColours)
            {
                colorShape.Matrix = ByteUtil.UnswizzlePalette(colorShape.Matrix, RealColour);
            }

            List<Rgba32> colors = new List<Rgba32>();

            for (int i = 0; i < RealColour; i++)
            {
                colors.Add(new Rgba32(colorShape.Matrix[i * 4], colorShape.Matrix[i * 4 + 1], colorShape.Matrix[i * 4 + 2], colorShape.Matrix[i * 4 + 3]));
            }

            return colors;
        }

        private List<Rgba32> GetColorTablePSP(ShapeImage newSSHImage, MatrixType matrixType)
        {
            var colorShape = GetShapeHeader(newSSHImage, matrixType).Value;

            int RealColour = colorShape.Width * colorShape.Height;

            if (colorShape.Size != 0)
            {
                RealColour = (colorShape.Size - 16) / 2;
            }

            if (newSSHImage.SwizzledColours)
            {
                throw new InvalidOperationException("Missing Swizzled Opperation for ABGR1555");
                //colorShape.Matrix = ByteUtil.UnswizzlePalette(colorShape.Matrix, RealColour);
            }

            List<Rgba32> colors = new List<Rgba32>();

            for (int i = 0; i < RealColour; i++)
            {
                byte low = colorShape.Matrix[(i *2)];
                byte high = colorShape.Matrix[(i *2) + 1];

                int value = low | (high << 8);

                //ABGR1555
                int a = (value >> 15) & 1;
                int b = (value >> 10) & 0x1F;
                int g = (value >> 5) & 0x1F;
                int r = value & 0x1F;

                byte R = (byte)((r << 3) | (r >> 2));
                byte G = (byte)((g << 3) | (g >> 2));
                byte B = (byte)((b << 3) | (b >> 2));
                byte A = (byte)(a * 255);

                Rgba32 NewColour = new Rgba32(R, G, B, 255);

                colors.Add(NewColour);
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

            if (TestAlpha)
            {
                newSSHImage.AlphaFix = true;
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

        private ShapeHeader? GetShapeHeader(ShapeImage newSSHImage, MatrixType Type)
        {
            for (int i = 0; i < newSSHImage.ShapeHeaders.Count; i++)
            {
                if (newSSHImage.ShapeHeaders[i].MatrixFormat == Type)
                {
                    return newSSHImage.ShapeHeaders[i];
                }
            }
            return null;
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
            temp.Image = Image.Load<Rgba32>(path);
            temp.colorsTable = ImageUtil.GetBitmapColorsFast(temp.Image).ToList();
            ShapeImages[i] = temp;
        }

        // Double the RGB to undo the PS2 GS half-bright colour store (stored 0x80 == 1.0). Returns true
        // if the image was doubled.
        //
        // guard=false (the default) doubles every pixel. The level, skybox, crowd and board/ski banks use
        // this: every image in them is half-bright. The crowd bank is premultiplied against its half-value
        // alpha, and the same doubling restores its opaque colour to full range.
        //
        // guard=true doubles only when the image's opaque texels (A >= 0x80) are all <= 0x80; a single
        // opaque channel over 0x80 marks the image full-range and leaves it unchanged. The opaque test
        // reads the stored range from the interior and skips transparent/premultiplied edges, mirroring
        // AlphaFix on the alpha channel. The shared PARTICLE sprite bank uses it for its per-image mix of
        // half-bright glow art (the ex06-09 explosion frames) and full-range sprites (fog0, envr, the
        // spray/needle sprites).
        public bool BrightenImage(int i, bool guard = false)
        {
            var TempImage = ShapeImages[i].Image;

            if (guard)
            {
                // Already stored full-range? (max opaque channel > 0x80) -> leave it untouched.
                bool alreadyFullRange = false;
                for (int y = 0; y < TempImage.Height && !alreadyFullRange; y++)
                {
                    for (int x = 0; x < TempImage.Width; x++)
                    {
                        Rgba32 c = TempImage[x, y];
                        if (c.A < 0x80) continue;   // ignore transparent / premultiplied-down edges
                        if (c.R > 0x80 || c.G > 0x80 || c.B > 0x80) { alreadyFullRange = true; break; }
                    }
                }
                if (alreadyFullRange) return false;
            }

            for (int y = 0; y < TempImage.Height; y++)
            {
                for (int x = 0; x < TempImage.Width; x++)
                {
                    Rgba32 color = TempImage[x, y];
                    // Clamp ceiling is 255, not 256: (byte)256 wraps to 0, which would turn
                    // any source channel >=129 black. Saturate to white instead.
                    color.R = (byte)(Math.Clamp(color.R * 2 - 1, 0, 255));
                    color.G = (byte)(Math.Clamp(color.G * 2 - 1, 0, 255));
                    color.B = (byte)(Math.Clamp(color.B * 2 - 1, 0, 255));

                    TempImage[x,y] = color;
                }
            }
            var tempimage = ShapeImages[i];
            tempimage.Image = TempImage;
            ShapeImages[i] = tempimage;
            return true;
        }

        public void DarkenImage(int i)
        {
            var TempImage = ShapeImages[i].Image;
            for (int y = 0; y < TempImage.Height; y++)
            {
                for (int x = 0; x < TempImage.Width; x++)
                {
                    Rgba32 color = TempImage[x, y];
                    color.R = (byte)(Math.Clamp((color.R + 1) / 2, 0, 255));
                    color.G = (byte)(Math.Clamp((color.G + 1) / 2, 0, 255));
                    color.B = (byte)(Math.Clamp((color.B + 1) / 2, 0, 255));

                    TempImage[x, y] = color;
                }
            }
            var tempimage = ShapeImages[i];
            tempimage.Image = TempImage;
            ShapeImages[i] = tempimage;
        }

        /// <summary>
        /// Extract a terrain lightmap's INTENSITY (A_S) as a grayscale image: rewrites RGB = alpha
        /// (alpha opaque). A convenience for viewing a lightmap, or for a neutral (uncoloured) multiply
        /// lightmap.
        ///
        /// SSX lightmaps are a two-term GS blend (2002 GDC "Light maps on the PS2" slide, (c) EA):
        ///   - alpha = A_S = max(C_L.R, C_L.G, C_L.B)  -> the per-texel light INTENSITY
        ///   - RGB   = C_S = C_D - (C_D x C_L) / A_S   -> a source-colour residual that bakes in the
        ///     base texture (dark/muddy on its own; the raw RGBA reads dark/orange because C_S is in RGB).
        /// The full lit pixel is (C_D - C_S) x A_S - this helper keeps ONLY A_S and DISCARDS C_S, so the
        /// result is the light's brightness without its colour. The coloured light C_L is entangled with
        /// the base texture and is not recoverable from the lightmap alone. Verified on GARI: alpha a
        /// smooth 76..255 gradient; RGB a faint warm tint with blue 0 (blue-dominant snow/sky light makes
        /// blue the max channel -> C_S.B = 0).
        ///
        /// Only valid for <see cref="MatrixType.FullColor"/> shapes - the format lightmaps use. For
        /// anything else the A_S-in-alpha layout does not hold (e.g. paletted shapes whose alpha may
        /// have been doubled by <see cref="AlphaFix"/>), so we leave the image untouched and report
        /// it rather than emit garbage. Returns true if the intensity was extracted.
        /// </summary>
        public bool ExtractLightmapIntensity(int i)
        {
            if (ShapeImages[i].MatrixType != MatrixType.FullColor)
            {
                Console.WriteLine($"ExtractLightmapIntensity: shape {i} ({ShapeImages[i].Shortname}) is "
                                  + $"{ShapeImages[i].MatrixType}, not FullColor - left as-is "
                                  + "(intensity-in-alpha only holds for FullColor lightmaps).");
                return false;
            }

            var img = ShapeImages[i].Image;
            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    byte l = img[x, y].A;   // intensity (A_S) is stored in alpha
                    img[x, y] = new Rgba32(l, l, l, 255);
                }
            }
            var tmp = ShapeImages[i];
            tmp.Image = img;
            ShapeImages[i] = tmp;
            return true;
        }

        public struct ShapeImage
        {
            internal int Offset;
            internal int Size;
            public string Shortname;
            public string Longname;
            internal List<ShapeHeader> ShapeHeaders;

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
            public bool MetalAlpha;
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
            ColorPallet_Xbox = 42,

            //PSP
            ColorPallet_PSP = 57,
            BC1_PSP = 69,
            EightBit_PSP = 93,

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

            Unknown1 = 124,

            EightBitCompressed = 130,
        }
    }
}
