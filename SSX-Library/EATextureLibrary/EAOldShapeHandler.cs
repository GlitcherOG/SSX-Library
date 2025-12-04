using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp;


namespace SSX_Library.EATextureLibrary
{
    public class EAOldShapeHandler
    {
        public string MagicWord;
        public int FileSize;
        public int ImageCount;
        public string Format;
        public string Group;
        public string EndingString;
        public List<ShapeImage> ShapeImages = new List<ShapeImage>();


        public struct ShapeImage
        {
            public int offset;
            public int size;
            public string shortname;
            public string longname;
            public List<ShapeHeader> sshShapeHeader;

            //Converted
            public List<Color> colorsTable;
            public Image Image;
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
