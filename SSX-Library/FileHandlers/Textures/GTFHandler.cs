using SSX_Library.Internal.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.FileHandlers.Textures
{
    /// <summary>
    /// Sony Cell/RSX GTF texture container (PS3). Used by SSX (2012) for every
    /// world texture under data/ps3/gameexplorer/worlds/textures/.
    ///
    /// The container is a CellGtfFileHeader followed by a CellGtfTextureAttribute
    /// whose tail is the CellGcmTexture descriptor. Everything in it is
    /// BIG-ENDIAN, and the pixel buffer starts at OffsetToTex (128 in practice).
    ///
    ///   0x00 u32 Version          0x01050000 on SSX 2012
    ///   0x04 u32 FileLength       payload bytes following the header
    ///   0x08 u32 NumTextures      1
    ///   0x0c u32 TextureId        RSX texture unit id; SSX 2012 reuses it as an
    ///                             is-normal-map flag (0 or 0x00010000)
    ///   0x10 u32 HeaderLength     offset to the pixel buffer
    ///   0x14 u32 TextureLength    pixel bytes, all mips and all cube faces
    ///   0x18 u8  TextureType      Cell GCM format | LN(0x20) | UN(0x40)
    ///   0x19 u8  NumMipmaps       level count, 1 == no mips
    ///   0x1a u8  Dimension        2 == 2D
    ///   0x1b u8  Cubemap          1 == six faces follow
    ///   0x1c u32 Remaps           channel remap word
    ///   0x20 u16 TextureWidth
    ///   0x22 u16 TextureHeight
    ///   0x24 u16 TextureDepth
    ///   0x26 u8  Location
    ///   0x27 u8  (pad)
    ///   0x28 u32 Pitch            row stride; 0 for the compressed formats
    ///   0x2c u32 Offset
    ///
    /// Layout rules that are not in the struct and have to be known:
    ///   * A linear (LN) texture keeps its mip-0 Pitch for EVERY mip level, so
    ///     short rows are padded out to Pitch and have to be unpacked.
    ///   * Cube faces are padded up to a 128-byte boundary, except the last one.
    ///   * The compressed formats ignore the SZ/LN bit - DXT blocks are always
    ///     stored linearly.
    /// </summary>
    public class GTFHandler
    {
        public int GTFVersion;
        public int FileLength;   // excluding the header
        public int NumTextures;
        public int TextureId;    // 0, or 0x00010000 on SSX 2012 normal maps
        public int HeaderLength; // also the offset to the pixel buffer
        public int TextureLength;
        public int TextureType;  // Cell GCM format byte, flags included
        public int NumMipmaps;
        public int Dimension;
        public int Cubemap;
        public int Remaps;
        public int TextureWidth;
        public int TextureHeight;
        public int TextureDepth;
        public int Location;
        public int Pitch;
        public int TextureOffset;

        // Cell GCM base texture formats (TextureType with LN/UN masked off).
        public const int CELL_GCM_TEXTURE_B8 = 0x81;
        public const int CELL_GCM_TEXTURE_A1R5G5B5 = 0x82;
        public const int CELL_GCM_TEXTURE_A4R4G4B4 = 0x83;
        public const int CELL_GCM_TEXTURE_R5G6B5 = 0x84;
        public const int CELL_GCM_TEXTURE_A8R8G8B8 = 0x85;
        public const int CELL_GCM_TEXTURE_COMPRESSED_DXT1 = 0x86;
        public const int CELL_GCM_TEXTURE_COMPRESSED_DXT23 = 0x87;
        public const int CELL_GCM_TEXTURE_COMPRESSED_DXT45 = 0x88;
        public const int CELL_GCM_TEXTURE_D8R8G8B8 = 0x9E;

        private const int CubeFaceAlign = 128;

        /// <summary>Base format with the LN (0x20) / UN (0x40) flags removed.</summary>
        public int BaseFormat => TextureType & 0x9F;

        /// <summary>True when the texel data is stored linearly rather than Morton-swizzled.</summary>
        public bool IsLinear => (TextureType & 0x20) != 0;

        public bool IsCubemap => Cubemap != 0;

        public int FaceCount => IsCubemap ? 6 : 1;

        /// <summary>SSX (2012) sets the otherwise-unused texture id on normal maps.</summary>
        public bool IsNormalMap => TextureId != 0;

        public void ReadHeader(Stream stream)
        {
            GTFVersion = StreamUtil.ReadUInt32(stream, true);
            FileLength = StreamUtil.ReadUInt32(stream, true);
            NumTextures = StreamUtil.ReadUInt32(stream, true);
            TextureId = StreamUtil.ReadUInt32(stream, true);
            HeaderLength = StreamUtil.ReadUInt32(stream, true);
            TextureLength = StreamUtil.ReadUInt32(stream, true);
            TextureType = StreamUtil.ReadUInt8(stream);
            NumMipmaps = StreamUtil.ReadUInt8(stream);
            Dimension = StreamUtil.ReadUInt8(stream);
            Cubemap = StreamUtil.ReadUInt8(stream);
            Remaps = StreamUtil.ReadUInt32(stream, true);
            TextureWidth = StreamUtil.ReadUInt16(stream, true);
            TextureHeight = StreamUtil.ReadUInt16(stream, true);
            TextureDepth = StreamUtil.ReadUInt16(stream, true);
            Location = StreamUtil.ReadUInt8(stream);
            StreamUtil.ReadUInt8(stream); // padding
            Pitch = StreamUtil.ReadUInt32(stream, true);
            TextureOffset = StreamUtil.ReadUInt32(stream, true);
        }

        public void GTFToDDS(string InputPath, string OutputPath)
        {
            byte[] dds;
            using (Stream stream = File.Open(InputPath, FileMode.Open, FileAccess.Read))
            {
                dds = GTFToDDS(stream, Path.GetFileName(InputPath));
            }

            string? dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(OutputPath, dds);
        }

        /// <summary>Reads a GTF and returns a complete .dds file (header + pixels).</summary>
        public byte[] GTFToDDS(Stream stream, string name = "<stream>")
        {
            ReadHeader(stream);

            if (NumTextures != 1)
            {
                throw new NotSupportedException(
                    $"{name}: GTF holds {NumTextures} textures, only single-texture files are supported.");
            }
            if (Dimension != 2)
            {
                throw new NotSupportedException(
                    $"{name}: GTF dimension {Dimension}, only 2D textures are supported.");
            }
            if (!IsCompressed(BaseFormat) && !IsUncompressed(BaseFormat))
            {
                throw new NotSupportedException(
                    $"{name}: unhandled Cell GCM texture format 0x{TextureType:X2}.");
            }
            if (IsUncompressed(BaseFormat) && !IsLinear)
            {
                // Morton/Z-order storage. Refuse rather than emit a scrambled image.
                throw new NotSupportedException(
                    $"{name}: swizzled (SZ) uncompressed texture, format 0x{TextureType:X2}; " +
                    "deswizzling is not implemented.");
            }

            int expected = ExpectedTextureLength();
            if (expected != TextureLength)
            {
                throw new InvalidDataException(
                    $"{name}: mip chain works out to {expected} bytes but the header says {TextureLength}.");
            }

            stream.Position = HeaderLength;
            // StreamUtil.ReadBytes returns a full-length buffer even on a short
            // read, so count the bytes here or a truncated GTF converts to a
            // DDS full of zeroes.
            byte[] raw = new byte[TextureLength];
            int read = 0;
            while (read < TextureLength)
            {
                int n = stream.Read(raw, read, TextureLength - read);
                if (n <= 0)
                {
                    break;
                }
                read += n;
            }
            if (read != TextureLength)
            {
                throw new InvalidDataException(
                    $"{name}: truncated, wanted {TextureLength} pixel bytes and got {read}.");
            }

            using (MemoryStream output = new MemoryStream())
            {
                WriteDDSHeader(output);
                int faceStride = FaceStride();
                for (int face = 0; face < FaceCount; face++)
                {
                    int cursor = face * faceStride;
                    for (int level = 0; level < NumMipmaps; level++)
                    {
                        int size = LevelStoredSize(level);
                        WriteLevel(output, raw, cursor, level);
                        cursor += size;
                    }
                }
                return output.ToArray();
            }
        }

        // -- geometry ---------------------------------------------------------

        private static bool IsCompressed(int baseFormat)
        {
            return baseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT1
                || baseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT23
                || baseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT45;
        }

        private static bool IsUncompressed(int baseFormat)
        {
            return BytesPerTexel(baseFormat) > 0;
        }

        private static int BytesPerTexel(int baseFormat)
        {
            switch (baseFormat)
            {
                case CELL_GCM_TEXTURE_B8: return 1;
                case CELL_GCM_TEXTURE_A1R5G5B5:
                case CELL_GCM_TEXTURE_A4R4G4B4:
                case CELL_GCM_TEXTURE_R5G6B5: return 2;
                case CELL_GCM_TEXTURE_A8R8G8B8:
                case CELL_GCM_TEXTURE_D8R8G8B8: return 4;
                default: return 0;
            }
        }

        private static int BlockBytes(int baseFormat)
        {
            return baseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT1 ? 8 : 16;
        }

        private int LevelWidth(int level) => Math.Max(1, TextureWidth >> level);

        private int LevelHeight(int level) => Math.Max(1, TextureHeight >> level);

        /// <summary>Bytes one mip level occupies as stored in the GTF.</summary>
        public int LevelStoredSize(int level)
        {
            int w = LevelWidth(level);
            int h = LevelHeight(level);
            if (IsCompressed(BaseFormat))
            {
                return ((w + 3) / 4) * ((h + 3) / 4) * BlockBytes(BaseFormat);
            }
            return StoredPitch() * h;
        }

        private int StoredPitch()
        {
            return Pitch > 0 ? Pitch : TextureWidth * BytesPerTexel(BaseFormat);
        }

        private int FaceSize()
        {
            int total = 0;
            for (int level = 0; level < NumMipmaps; level++)
            {
                total += LevelStoredSize(level);
            }
            return total;
        }

        private int FaceStride()
        {
            int face = FaceSize();
            if (!IsCubemap)
            {
                return face;
            }
            return (face + CubeFaceAlign - 1) / CubeFaceAlign * CubeFaceAlign;
        }

        /// <summary>What TextureLength should be, given the header's geometry.</summary>
        public int ExpectedTextureLength()
        {
            int face = FaceSize();
            if (!IsCubemap)
            {
                return face;
            }
            // Every face but the last is padded up to the 128-byte boundary.
            return FaceStride() * (FaceCount - 1) + face;
        }

        // -- pixels -----------------------------------------------------------

        private void WriteLevel(Stream output, byte[] raw, int offset, int level)
        {
            if (IsCompressed(BaseFormat))
            {
                output.Write(raw, offset, LevelStoredSize(level));
                return;
            }

            int bpp = BytesPerTexel(BaseFormat);
            int w = LevelWidth(level);
            int h = LevelHeight(level);
            int rowBytes = w * bpp;
            int stride = StoredPitch();
            byte[] row = new byte[rowBytes];
            for (int y = 0; y < h; y++)
            {
                Array.Copy(raw, offset + y * stride, row, 0, rowBytes);
                SwapTexelBytes(row, bpp);
                output.Write(row, 0, rowBytes);
            }
        }

        /// <summary>
        /// GTF stores multi-byte texels big-endian; the DDS channel masks below
        /// assume little-endian, so each texel is byte-reversed. Single-byte
        /// formats need nothing.
        /// </summary>
        private static void SwapTexelBytes(byte[] row, int bpp)
        {
            if (bpp < 2)
            {
                return;
            }
            for (int i = 0; i + bpp <= row.Length; i += bpp)
            {
                Array.Reverse(row, i, bpp);
            }
        }

        // -- DDS container ----------------------------------------------------

        private const int DDSD_CAPS = 0x1;
        private const int DDSD_HEIGHT = 0x2;
        private const int DDSD_WIDTH = 0x4;
        private const int DDSD_PITCH = 0x8;
        private const int DDSD_PIXELFORMAT = 0x1000;
        private const int DDSD_MIPMAPCOUNT = 0x20000;
        private const int DDSD_LINEARSIZE = 0x80000;

        private const int DDSCAPS_COMPLEX = 0x8;
        private const int DDSCAPS_TEXTURE = 0x1000;
        private const int DDSCAPS_MIPMAP = 0x400000;
        private const int DDSCAPS2_CUBEMAP_ALLFACES = 0xFE00;

        private const int DDPF_ALPHAPIXELS = 0x1;
        private const int DDPF_FOURCC = 0x4;
        private const int DDPF_RGB = 0x40;
        private const int DDPF_LUMINANCE = 0x20000;

        private void WriteDDSHeader(Stream output)
        {
            int flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT;
            int caps = DDSCAPS_TEXTURE;
            if (NumMipmaps > 1)
            {
                flags |= DDSD_MIPMAPCOUNT;
                caps |= DDSCAPS_MIPMAP | DDSCAPS_COMPLEX;
            }
            int caps2 = 0;
            if (IsCubemap)
            {
                caps |= DDSCAPS_COMPLEX;
                caps2 = DDSCAPS2_CUBEMAP_ALLFACES;
            }

            int pitchOrLinearSize;
            int pfFlags;
            string fourCC = "\0\0\0\0";
            int bitCount = 0, rMask = 0, gMask = 0, bMask = 0, aMask = 0;

            if (IsCompressed(BaseFormat))
            {
                flags |= DDSD_LINEARSIZE;
                pitchOrLinearSize = ((TextureWidth + 3) / 4) * ((TextureHeight + 3) / 4)
                                    * BlockBytes(BaseFormat);
                pfFlags = DDPF_FOURCC;
                fourCC = BaseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT1 ? "DXT1"
                       : BaseFormat == CELL_GCM_TEXTURE_COMPRESSED_DXT23 ? "DXT3"
                       : "DXT5";
            }
            else
            {
                flags |= DDSD_PITCH;
                pitchOrLinearSize = TextureWidth * BytesPerTexel(BaseFormat);
                switch (BaseFormat)
                {
                    case CELL_GCM_TEXTURE_B8:
                        pfFlags = DDPF_LUMINANCE; bitCount = 8; rMask = 0xFF;
                        break;
                    case CELL_GCM_TEXTURE_R5G6B5:
                        pfFlags = DDPF_RGB; bitCount = 16;
                        rMask = 0xF800; gMask = 0x07E0; bMask = 0x001F;
                        break;
                    case CELL_GCM_TEXTURE_A1R5G5B5:
                        pfFlags = DDPF_RGB | DDPF_ALPHAPIXELS; bitCount = 16;
                        rMask = 0x7C00; gMask = 0x03E0; bMask = 0x001F; aMask = unchecked((int)0x8000);
                        break;
                    case CELL_GCM_TEXTURE_A4R4G4B4:
                        pfFlags = DDPF_RGB | DDPF_ALPHAPIXELS; bitCount = 16;
                        rMask = 0x0F00; gMask = 0x00F0; bMask = 0x000F; aMask = unchecked((int)0xF000);
                        break;
                    case CELL_GCM_TEXTURE_A8R8G8B8:
                        pfFlags = DDPF_RGB | DDPF_ALPHAPIXELS; bitCount = 32;
                        rMask = 0x00FF0000; gMask = 0x0000FF00; bMask = 0x000000FF;
                        aMask = unchecked((int)0xFF000000);
                        break;
                    default: // CELL_GCM_TEXTURE_D8R8G8B8
                        pfFlags = DDPF_RGB; bitCount = 32;
                        rMask = 0x00FF0000; gMask = 0x0000FF00; bMask = 0x000000FF;
                        break;
                }
            }

            StreamUtil.WriteString(output, "DDS ");
            StreamUtil.WriteInt32(output, 124);              // dwSize
            StreamUtil.WriteInt32(output, flags);
            StreamUtil.WriteInt32(output, TextureHeight);
            StreamUtil.WriteInt32(output, TextureWidth);
            StreamUtil.WriteInt32(output, pitchOrLinearSize);
            StreamUtil.WriteInt32(output, 0);                // dwDepth
            StreamUtil.WriteInt32(output, NumMipmaps);
            StreamUtil.WriteBytes(output, new byte[44]);     // dwReserved1[11]

            StreamUtil.WriteInt32(output, 32);               // DDS_PIXELFORMAT.dwSize
            StreamUtil.WriteInt32(output, pfFlags);
            StreamUtil.WriteString(output, fourCC);
            StreamUtil.WriteInt32(output, bitCount);
            StreamUtil.WriteInt32(output, rMask);
            StreamUtil.WriteInt32(output, gMask);
            StreamUtil.WriteInt32(output, bMask);
            StreamUtil.WriteInt32(output, aMask);

            StreamUtil.WriteInt32(output, caps);
            StreamUtil.WriteInt32(output, caps2);
            StreamUtil.WriteInt32(output, 0);                // dwCaps3
            StreamUtil.WriteInt32(output, 0);                // dwCaps4
            StreamUtil.WriteInt32(output, 0);                // dwReserved2
        }

        public void DDSToGTF(string path)
        {
            throw new NotImplementedException("DDS -> GTF is not implemented.");
        }
    }
}
