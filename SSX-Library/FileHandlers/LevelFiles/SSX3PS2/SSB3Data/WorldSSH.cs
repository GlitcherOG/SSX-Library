using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSX_Library.EATextureLibrary;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData
{
    public class WorldSSH
    {
        public byte MatrixFormat;
        public int Size;
        public int Width;
        public int Height;
        public int Xaxis;
        public int Yaxis;
        public int LXPos;
        public bool flag1;
        public bool flag2;
        public bool flag3;
        public bool flag4;
        public int TYPos;
        public int Mipmaps; //Unit4

        bool AlphaFix;

        public byte[] Matrix;
        public SSHColourTable sshTable;
        public Image<Rgba32> bitmap;

        public void Load(Stream stream)
        {
            stream.Position = 0;

            MatrixFormat = StreamUtil.ReadUInt8(stream);

            Size = StreamUtil.ReadUInt24(stream);

            Width = StreamUtil.ReadInt16(stream);

            Height = StreamUtil.ReadInt16(stream);

            Xaxis = StreamUtil.ReadInt16(stream);

            Yaxis = StreamUtil.ReadInt16(stream);

            //Add Other Flags Later
            LXPos = StreamUtil.ReadInt12(stream);

            TYPos = StreamUtil.ReadInt12(stream);

            int RealSize;

            if (Size != 0)
            {
                RealSize = Size - 0x80;
            }
            else
            {
                RealSize = Width * Height;
                if (MatrixFormat == 5)
                {
                    RealSize = RealSize * 4;
                }
            }

            stream.Position = 0x80;

            //Read Matrix
            var tempByte = new byte[RealSize];
            stream.Read(tempByte, 0, tempByte.Length);
            Matrix = tempByte;

            if (MatrixFormat == 1)
            {
                Matrix = ByteUtil.Unswizzle4bpp(Matrix, Width, Height);
            }

            if (MatrixFormat == 2)
            {
                Matrix = ByteUtil.Unswizzle8(Matrix, Width, Height);
            }

            //INDEXED COLOUR
            if (MatrixFormat == 2 || MatrixFormat == 1)
            {
                int PosPallet = (int)stream.Position;

                stream.Position = Size + 0x1;

                sshTable = new SSHColourTable();

                sshTable.Size = StreamUtil.ReadUInt24(stream);

                sshTable.Width = StreamUtil.ReadInt16(stream);

                sshTable.Height = StreamUtil.ReadInt16(stream);

                sshTable.Total = StreamUtil.ReadInt16(stream);

                sshTable.Format = StreamUtil.ReadUInt32(stream);

                sshTable.colorTable = new List<Rgba32>();

                stream.Position = PosPallet + 0x80;

                var Matrix = StreamUtil.ReadBytes(stream, sshTable.Total * 4);

                if (MatrixFormat == 2)
                {
                    Matrix = ByteUtil.UnswizzlePalette(Matrix, sshTable.Total);
                }

                for (int i = 0; i < sshTable.Total; i++)
                {
                    byte R = Matrix[i * 4];
                    byte G = Matrix[i * 4+1];
                    byte B = Matrix[i * 4+2];
                    byte A = Matrix[i * 4+3];
                    sshTable.colorTable.Add(new Rgba32(R, G, B, A));
                }

                int Max = 0;
                //Alpha Fix
                for (int a = 0; a < sshTable.colorTable.Count; a++)
                {
                    if (Max < sshTable.colorTable[a].A)
                    {
                        Max = sshTable.colorTable[a].A;
                    }
                }
                if (Max <= 128)
                {
                    AlphaFix = true;

                    for (int a = 0; a < sshTable.colorTable.Count; a++)
                    {
                        var TempColour = sshTable.colorTable[a];
                        int A = TempColour.A * 2;
                        if (A > 255)
                        {
                            A = 255;
                        }
                        else if (A < 0)
                        {
                            A = 0;
                        }
                        TempColour = new Rgba32(TempColour.R, TempColour.G, TempColour.B, (byte)A);
                        sshTable.colorTable[a] = TempColour;
                    }
                }
            }

            //Colour Correction
            int tempRead = stream.ReadByte();
            if (tempRead == 105)
            {
                Console.WriteLine("METAL ERROR");
            }
            else
            {
                stream.Position -= 1;
            }

            //Create Bitmap Image
            bitmap = new Image<Rgba32>(Width, Height);
            if (MatrixFormat == 1)
            {
                bitmap = EADecode.DecodeMatrix1(Matrix, sshTable.colorTable, Width, Height);
            }
            else
            if (MatrixFormat == 2)
            {
                bitmap = EADecode.DecodeMatrix2(Matrix, sshTable.colorTable, Width, Height);
            }
            else
            if (MatrixFormat == 5)
            {
                bitmap = EADecode.DecodeMatrix5(Matrix, Width, Height);
            }
            else
            {
                //MessageBox.Show("Error reading File" + MatrixFormat.ToString());
            }

        }
        public void SaveImage(string path)
        {
            bitmap.SaveAsPng(path);
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
    }
}
