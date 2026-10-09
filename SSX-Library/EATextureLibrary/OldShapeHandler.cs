using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System.Drawing;
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

        public bool GCFile = false;


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

                    if(ConsoleVersion == TextureType.OldGC)
                    {
                        GCFile = true;
                    }

                    FileSize = StreamUtil.ReadUInt32(stream);

                    ImageCount = StreamUtil.ReadUInt32(stream, GCFile);

                    Format = StreamUtil.ReadString(stream, 4);

                    for (int i = 0; i < ImageCount; i++)
                    {
                        ShapeImage tempImage = new ShapeImage();

                        tempImage.Shortname = StreamUtil.ReadString(stream, 4);

                        tempImage.Offset = StreamUtil.ReadUInt32(stream, GCFile);

                        //SSX OG Simple Check onsize should work

                        //SSX Tricky Requires each image being read correctly in terms of offset but for end it has Buy ERTS
                        //Buy ERTS for group ending

                        //SSX 3
                        //Mix of no ERTS for group ending and ERTS for group ending

                        ShapeImages.Add(tempImage);
                    }

                    long SavePos = stream.Position;

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

                    stream.Position = SavePos;

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
                        shape.Size = StreamUtil.ReadUInt24(stream, GCFile);

                        shape.Width = StreamUtil.ReadInt16(stream, GCFile);

                        shape.Height = StreamUtil.ReadInt16(stream, GCFile);

                        shape.Xaxis = StreamUtil.ReadInt16(stream, GCFile);

                        shape.Yaxis = StreamUtil.ReadInt16(stream, GCFile);

                        //Add Other Flags Later
                        shape.Flags = StreamUtil.ReadInt16(stream, GCFile);

                        stream.Position += 2;

                        if (shape.Size == 0 || shape.MatrixFormat == MatrixType.LongName)
                        {
                            int RealSize = shape.Width * shape.Height;
                            if (shape.MatrixFormat == MatrixType.ColorPallet || shape.MatrixFormat == MatrixType.ColorPallet_Xbox || shape.MatrixFormat == MatrixType.FullColor || shape.MatrixFormat == MatrixType.BGRA)
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
                        shape.Size = StreamUtil.ReadUInt24(stream, GCFile);

                        shape.Width = StreamUtil.ReadInt32(stream, GCFile);

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
                if (imageMatrix.Matrix != null && imageMatrix.Matrix.Length > 0 && (imageMatrix.MatrixFormat == MatrixType.EightBitCompressed || imageMatrix.MatrixFormat == MatrixType.BGRACompressed))
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

                if (tempImage.MatrixType == MatrixType.EightBitGC)
                {
                    var colorShape = GetShapeHeader(tempImage, MatrixType.ColorPallet_GC);
                    tempImage.colorsTable = GetColorTableGC(tempImage, MatrixType.ColorPallet_GC);
                }

                if (tempImage.MatrixType == MatrixType.EightBitXbox)
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
                    case MatrixType.EightBitGC:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.N64I8Deswizzle(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        }
                        tempImage.Image = EADecode.DecodeMatrix2(imageMatrix.Matrix, tempImage.colorsTable, imageMatrix.Width, imageMatrix.Height);
                        break;
                    case MatrixType.FullColor:
                        tempImage.Image = EADecode.DecodeMatrix5(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.BGR5A3:
                        if (tempImage.SwizzledImage)
                        {
                            imageMatrix.Matrix = ByteUtil.N64_BGR5A3_Deswizzle(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        }
                        tempImage.Image = EADecode.DecodeMatrix21(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
                        tempImage.colorsTable = ImageUtil.GetBitmapColorsFast(tempImage.Image).ToList();
                        break;
                    case MatrixType.N64_CMPR:
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
                    case MatrixType.BC3:
                        tempImage.Image = EADecode.DecodeMatrix98(imageMatrix.Matrix, imageMatrix.Width, imageMatrix.Height);
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
                    case MatrixType.BGRACompressed:
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
                case TextureType.OldPSP: //PSP
                    MagicWord = "SHPM";
                    break;
            }

            if (ConsoleVersion == TextureType.OldGC)
            {
                GCFile = true;
            }

            //Write Header
            byte[] tempByte = new byte[4];
            Stream stream = new MemoryStream();

            StreamUtil.WriteString(stream, MagicWord, 4);

            long SizePos = stream.Position;
            tempByte = new byte[4];
            stream.Write(tempByte, 0, tempByte.Length);

            StreamUtil.WriteInt32(stream, ShapeImages.Count, GCFile);

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
                StreamUtil.WriteInt32(stream, TempPos, GCFile);
                stream.Position = TempPos;

                var TempMatrix = ImageWrite(ShapeImages[i]);

                StreamUtil.WriteBytes(stream, TempMatrix);

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
            if (shapeImage.MetalAlpha)
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

            //Limit Colours for Saving
            shapeImage.colorsTable = ImageUtil.GetBitmapColorsFast(shapeImage.Image).ToList();

            //if metal bin combine images and then reduce
            if (shapeImage.colorsTable.Count > 16 && shapeImage.MatrixType == MatrixType.FourBit)
            {
                Console.WriteLine("Over 16 Colour Limit " + shapeImage.Shortname);
                shapeImage.Image = ImageUtil.ReduceBitmapColorsFast(shapeImage.Image, 16);
            }
            if (shapeImage.colorsTable.Count > 256 && (shapeImage.MatrixType == MatrixType.EightBit || shapeImage.MatrixType == MatrixType.EightBitCompressed
                || shapeImage.MatrixType == MatrixType.EightBitXbox || shapeImage.MatrixType == MatrixType.EightBitGC))
            {
                Console.WriteLine("Over 256 Colour Limit " + shapeImage.Shortname);
                // Keep transparency when limiting indexed textures; the RGB-only reducer drops alpha.
                shapeImage.Image.Mutate(x => x.Quantize(new WuQuantizer(new QuantizerOptions { MaxColors = 256 })));
            }
            shapeImage.colorsTable = ImageUtil.GetBitmapColorsFast(shapeImage.Image).ToList();

            var Matrix = new byte[0];
            var Colours = new List<Rgba32>();

            //Process into image
            switch (shapeImage.MatrixType)
            {
                case MatrixType.FourBit:
                    var EncodedImage = EAEncode.EncodeMatrix1(shapeImage.Image);
                    Matrix = EncodedImage.Matrix;
                    Colours = EncodedImage.ColourTable;
                    if (shapeImage.SwizzledImage)
                    {
                        //Swizzle the Image
                        Matrix = ByteUtil.Swizzle4bpp(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                    }
                    break;
                case MatrixType.EightBit:
                case MatrixType.EightBitCompressed:
                case MatrixType.EightBitXbox:
                case MatrixType.EightBit_PSP:
                    var EncodedImage1 = EAEncode.EncodeMatrix2(shapeImage.Image);
                    Matrix = EncodedImage1.Matrix;
                    Colours = EncodedImage1.ColourTable;
                    if (shapeImage.SwizzledImage)
                    {
                        Matrix = ByteUtil.Swizzle8(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                    }
                    break;
                case MatrixType.EightBitGC:
                    var EncodedImage2 = EAEncode.EncodeMatrix2(shapeImage.Image);
                    Matrix = EncodedImage2.Matrix;
                    Colours = EncodedImage2.ColourTable;
                    if (shapeImage.SwizzledImage)
                    {
                        Matrix = ByteUtil.N64I8Swizzle(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                    }
                    break;
                case MatrixType.FullColor:
                    Matrix = EAEncode.EncodeMatrix5(shapeImage.Image);
                    break;
                case MatrixType.BGR5A3:
                    Matrix = EAEncode.EncodeMatrix21(shapeImage.Image);
                    if (shapeImage.SwizzledImage)
                    {
                        Matrix = ByteUtil.N64_BGR5A3_Swizzle(Matrix, shapeImage.Image.Width, shapeImage.Image.Height);
                    }
                    break;
                case MatrixType.N64_CMPR:
                    Matrix = EAEncode.EncodeMatrix30(shapeImage.Image);
                    break;
                case MatrixType.BC1_PSP:
                case MatrixType.BC1:
                    Matrix = EAEncode.EncodeMatrixDXT1(shapeImage.Image);
                    break;
                case MatrixType.BC2:
                    Matrix = EAEncode.EncodeMatrix97(shapeImage.Image);
                    break;
                case MatrixType.BC3:
                    Matrix = EAEncode.EncodeMatrix98(shapeImage.Image);
                    break;
                case MatrixType.BGRA4444:
                    Matrix = EAEncode.EncodeMatrix109(shapeImage.Image);
                    break;
                case MatrixType.BGR565:
                    Matrix = EAEncode.EncodeMatrix120(shapeImage.Image);
                    break;
                case MatrixType.BGRACompressed:
                case MatrixType.BGRA:
                    Matrix = EAEncode.EncodeMatrix125(shapeImage.Image);
                    break;
                default:
                    Console.WriteLine(shapeImage.MatrixType + " Unknown Matrix");
                    break;
            }

            // The texel indices and written palette must use the encoder's same first-seen ordering.
            if (Colours.Count > 0)
            {
                shapeImage.colorsTable = Colours;
                if (shapeImage.MatrixType == MatrixType.EightBit)
                {
                    // Retail PS2 8-bit shapes carry a full 256-entry palette, even for small images.
                    while (shapeImage.colorsTable.Count < 256)
                    {
                        shapeImage.colorsTable.Add(new Rgba32());
                    }
                }
            }

            //Compress Image
            if (shapeImage.MatrixType == MatrixType.EightBitCompressed || shapeImage.MatrixType == MatrixType.BGRACompressed)
            {
                //Compress Image
                byte[] TempBytes = Refpack.Compress(Matrix);
                Matrix = TempBytes;
            }

            // Include alignment padding in the chunk extent so the next palette header can be read.
            WriteImageHeader(stream, shapeImage, StreamUtil.AlignbyMath(Matrix.Length + 16, 16));

            StreamUtil.WriteBytes(stream, Matrix);

            //Might not be needed
            StreamUtil.AlignBy16(stream);

            if (shapeImage.MatrixType == MatrixType.FourBit || shapeImage.MatrixType == MatrixType.EightBit || shapeImage.MatrixType == MatrixType.EightBitCompressed || shapeImage.MatrixType == MatrixType.EightBitXbox)
            {
                //Generate Colour Table Matrix
                WriteColourTable(stream, shapeImage);

                StreamUtil.AlignBy16(stream);
            }

            if (shapeImage.MatrixType == MatrixType.EightBitGC)
            {
                //Generate Colour Table Matrix
                WriteColourTableGC(stream, shapeImage);

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
            if (shapeImage.Longname != "" && shapeImage.Longname != null)
            {
                stream.WriteUInt32((byte)MatrixType.LongName, ByteOrder.LittleEndian);
                stream.WriteAsciiWithLength(shapeImage.Longname, 12);
            }

            // Seeking for alignment alone does not extend a MemoryStream's final chunk.
            stream.SetLength(stream.Position);
            stream.Position = 0;
            return StreamUtil.ReadBytes(stream, (int)stream.Length);
        }

        public void WriteImageHeader(Stream stream, ShapeImage image, int DataSize)
        {
            StreamUtil.WriteUInt8(stream, (int)image.MatrixType);

            StreamUtil.WriteInt24(stream, DataSize, GCFile);

            StreamUtil.WriteInt16(stream, image.Image.Width, GCFile);

            StreamUtil.WriteInt16(stream, image.Image.Height, GCFile);

            StreamUtil.WriteInt16(stream, image.Xaxis, GCFile);

            StreamUtil.WriteInt16(stream, image.Yaxis, GCFile);

            int Flags = 0;
            Flags += (image.SwizzledImage ? 8192 : 0);

            StreamUtil.WriteInt16(stream, Flags, GCFile);

            stream.Position += 2;
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

            WriteColourHeader(stream, image, MatrixSize + 16, 33);

            StreamUtil.WriteBytes(stream, Matrix);
        }

        public void WriteColourTableGC(Stream stream, ShapeImage image)
        {
            int MatrixSize = StreamUtil.AlignbyMath(2 * image.colorsTable.Count, 16);

            byte[] Matrix = new byte[MatrixSize];

            for (int i = 0; i < image.colorsTable.Count; i++)
            {
                Rgba32 color = image.colorsTable[i];

                ushort value;

                // RGB5
                // 1RRRRRGGGGGBBBBB
                // Used when alpha is fully opaque.
                if (color.A == 255)
                {
                    ushort r = (ushort)(color.R >> 3);
                    ushort g = (ushort)(color.G >> 3);
                    ushort b = (ushort)(color.B >> 3);

                    value = (ushort)(
                        0x8000 |
                        (r << 10) |
                        (g << 5) |
                        b
                    );
                }
                // A3R4G4B4
                // 0AAARRRRGGGGBBBB
                else
                {
                    ushort a = (ushort)(color.A >> 5);
                    ushort r = (ushort)(color.R >> 4);
                    ushort g = (ushort)(color.G >> 4);
                    ushort b = (ushort)(color.B >> 4);

                    value = (ushort)(
                        (a << 12) |
                        (r << 8) |
                        (g << 4) |
                        b
                    );
                }

                // GameCube texture data is big-endian.
                Matrix[i * 2] = (byte)(value >> 8);
                Matrix[i * 2 + 1] = (byte)(value & 0xFF);
            }

            //if (image.SwizzledColours)
            //{
            //    //Swizzle Colours
            //    Matrix = ByteUtil.SwizzlePalette(Matrix, image.colorsTable.Count);
            //    //Limit Matrix to Remove Bloat
            //}

            WriteColourHeader(stream, image, Matrix.Length + 16, 50);

            StreamUtil.WriteBytes(stream, Matrix);
        }

        public void WriteColourHeader(Stream stream, ShapeImage image, int Size, int Matrix)
        {
            StreamUtil.WriteUInt8(stream, Matrix);

            StreamUtil.WriteInt24(stream, Size, GCFile);

            StreamUtil.WriteInt16(stream, image.colorsTable.Count, GCFile);

            StreamUtil.WriteInt16(stream, 1, GCFile);

            StreamUtil.WriteInt16(stream, image.colorsTable.Count, GCFile);

            StreamUtil.WriteInt16(stream, 0, GCFile);

            int Flags = 0;
            Flags += (image.SwizzledColours ? 8192 : 0);

            StreamUtil.WriteInt32(stream, Flags, GCFile);
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

        private List<Rgba32> GetColorTableGC(ShapeImage newSSHImage, MatrixType matrixType)
        {
            var colorShape = GetShapeHeader(newSSHImage, matrixType).Value;

            //if(colorShape.MatrixFormat == MatrixType.Unknown)
            //{
            //    colorShape = GetShapeHeader(newSSHImage, MatrixType.ColorPallet_Xbox).Value;
            //}
            int RealColour = colorShape.Width * colorShape.Height;

            if (colorShape.Size != 0)
            {
                RealColour = (colorShape.Size - 16) / 2;
            }

            if (newSSHImage.SwizzledColours)
            {
                colorShape.Matrix = ByteUtil.UnswizzlePalette(colorShape.Matrix, RealColour);
            }

            List<Rgba32> colors = new List<Rgba32>();

            for (int i = 0; i < RealColour; i++)
            {
                ushort value = (ushort)(
                    (colorShape.Matrix[i * 2] << 8) |
                    colorShape.Matrix[i * 2 + 1]);

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

                colors.Add(new Rgba32(r, g, b, a));
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

        public void ExtractSingleMetalImage(string path, int i)
        {
            ShapeImages[i].Metal.SaveAsPng(path);
        }

        public void LoadSingleMetalImage(string path, int i)
        {
            var temp = ShapeImages[i];
            temp.Metal = Image.Load(path).CloneAs<A8>();
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
            BGR5A3 = 21,
            EightBitGC = 25,
            N64_CMPR = 30,

            ColorPallet = 33,
            ColorPallet_Xbox = 42,
            ColorPallet_GC = 50,

            //PSP
            ColorPallet_PSP = 57,
            BC1_PSP = 69,
            EightBit_PSP = 93,

            //Xbox
            BC1 = 96,
            BC2 = 97,
            BC3 = 98,
            BGRA4444 = 109,
            BGR565 = 120,
            EightBitXbox = 123,
            BGRA = 125,

            //Other
            MetalAlpha = 105,
            LongName = 112,

            Unknown1 = 124,

            EightBitCompressed = 130,
            BGRACompressed = 253,
        }
    }
}
