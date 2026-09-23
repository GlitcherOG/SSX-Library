using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.EATextureLibrary;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;

namespace SSXLibrary.FileHandlers.LevelFiles.SSXOnTourPS2.SSBOnTourData
{
    public class WorldSSH
    {
        internal List<ShapeHeader> ShapeHeaders;

        //Converted
        public List<Rgba32> colorsTable;
        public Image<Rgba32> Image;
        public MatrixType matrixType;
        public bool Compressed;
        public bool SwizzledImage;
        public bool SwizzledColours;
        public bool AlphaFix;
        public byte[] Matrix;

        public void Load(Stream stream)
        {
            stream.Position = 0;

            ShapeHeaders = new List<ShapeHeader>();
            Image = new Image<Rgba32>(1, 1);

            while (stream.Position < stream.Length)
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

                stream.Position += 96;
                
                if (shape.Size == 0)
                {
                    shape.Matrix = StreamUtil.ReadBytes(stream, shape.DataSize);
                }
                else
                {
                    shape.Matrix = StreamUtil.ReadBytes(stream, shape.Size - shape.U2);
                }

                //StreamUtil.AlignBy16(stream);

                if(shape.MatrixFormat==MatrixType.FourBit)
                {
                    
                    return;
                }

                ShapeHeaders.Add(shape);
            }

            //Get Matrix Type
            matrixType = GetShapeMatrixType();
            var imageMatrix = GetShapeHeader(matrixType);

            Compressed = (imageMatrix.Flags1 & 2) == 2;
            SwizzledImage = (imageMatrix.Flags2 & 64) == 64;

            //Uncompress
            if (Compressed)
            {
                Matrix = Refpack.Decompress(imageMatrix.Matrix);
            }

            //Process Colors
            //Todo Check If Type is here instead
            if (matrixType == MatrixType.FourBit || matrixType == MatrixType.EightBit)
            {
                var colorShape = GetShapeHeader(MatrixType.ColorPallet);
                SwizzledColours = (colorShape.Flags2 & 64) == 64;
                colorsTable = GetColorTable();
                AlphaFixVoid();
            }


            //Process into image
            switch (matrixType)
            {
                case MatrixType.FourBit:
                    if (SwizzledImage)
                    {
                        imageMatrix.Matrix = ByteUtil.Unswizzle4bpp(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                    }
                    Image = EADecode.DecodeMatrix1(imageMatrix.Matrix, colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                    break;
                case MatrixType.EightBit:
                    if (SwizzledImage)
                    {
                        imageMatrix.Matrix = ByteUtil.Unswizzle8(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                    }
                    Image = EADecode.DecodeMatrix2(imageMatrix.Matrix, colorsTable, imageMatrix.XSize, imageMatrix.YSize);
                    break;
                case MatrixType.FullColor:
                    Image = EADecode.DecodeMatrix5(imageMatrix.Matrix, imageMatrix.XSize, imageMatrix.YSize);
                    colorsTable = ImageUtil.GetBitmapColorsFast(Image).ToList();
                    break;
                default:
                    Console.WriteLine(matrixType + " Unknown Matrix");
                    break;
            }

        }

        private List<Rgba32> GetColorTable()
        {
            var colorShape = GetShapeHeader(MatrixType.ColorPallet);
            List<Rgba32> colors = new List<Rgba32>();

            if (SwizzledColours)
            {
                colorShape.Matrix = ByteUtil.UnswizzlePalette(colorShape.Matrix, colorShape.XSize);
            }

            for (int i = 0; i < colorShape.XSize * colorShape.YSize; i++)
            {
                colors.Add(new Rgba32(colorShape.Matrix[i * 4], colorShape.Matrix[i * 4 + 1], colorShape.Matrix[i * 4 + 2], colorShape.Matrix[i * 4 + 3]));
            }

            return colors;
        }

        private void AlphaFixVoid()
        {
            bool TestAlpha = true;

            for (int i = 0; i < colorsTable.Count; i++)
            {
                if (colorsTable[i].A > 0x80)
                {
                    TestAlpha = false;
                    break;
                }
            }
            AlphaFix = true;

            if (TestAlpha)
            {
                for (int i = 0; i < colorsTable.Count; i++)
                {
                    var TempColour = colorsTable[i];
                    int A = TempColour.A * 2;
                    if (A > 255)
                    {
                        A = 255;
                    }
                    TempColour.A = (byte)A;
                    colorsTable[i] = TempColour;
                }
            }
        }

        private ShapeHeader GetShapeHeader(MatrixType Type)
        {
            for (int i = 0; i < ShapeHeaders.Count; i++)
            {
                if (ShapeHeaders[i].MatrixFormat == Type)
                {
                    return ShapeHeaders[i];
                }
            }
            return new ShapeHeader();
        }

        private MatrixType GetShapeMatrixType()
        {
            for (int i = 0; i < ShapeHeaders.Count; i++)
            {
                if (ShapeHeaders[i].MatrixFormat == MatrixType.FourBit || ShapeHeaders[i].MatrixFormat == MatrixType.EightBit ||
                    ShapeHeaders[i].MatrixFormat == MatrixType.FullColor)
                {
                    return (MatrixType)ShapeHeaders[i].MatrixFormat;
                }
            }

            return (MatrixType)ShapeHeaders[0].MatrixFormat;
        }

        public void SaveImage(string path)
        {
            Image.SaveAsPng(path);
        }
        public struct SSHColourTable
        {
            public int Size;
            public int Width;
            public int Height;
            public int Total;
            public int Format;
            public List<Rgba32> colorTable;
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
