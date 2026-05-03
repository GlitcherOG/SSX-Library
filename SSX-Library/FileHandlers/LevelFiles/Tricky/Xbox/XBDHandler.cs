using ICSharpCode.SharpZipLib.Core;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.FileHandlers.LevelFiles.Tricky.Xbox
{
    public class XBDHandler
    {
        public uint Magic;
        public uint U0;
        public uint NumPatches;
        public uint NumU1;

        public uint NumU2;
        public uint NumU3;
        public uint NumU4;
        public uint NumU5;

        public uint NumU6;
        public uint NumU7;
        public uint NumU8;
        public uint NumU9;

        public uint NumU10;
        public uint NumU11;
        public uint NumU12;
        public uint NumU13;

        public uint NumU14;
        public uint NumU15;
        public uint NumU16;
        public uint NumU17;

        public uint OffsetPatches;
        public uint OffsetPatchSourcePoints;
        public uint OffsetU1;
        public uint OffsetU2;

        public uint OffsetU3;
        public uint OffsetU4;
        public uint OffsetU5;
        public uint OffsetU6;

        public uint OffsetU7;
        public uint OffsetU8;
        public uint OffsetU9;
        public uint OffsetU10;

        public uint OffsetU11;
        public uint OffsetU12;
        public uint OffsetU13;
        public uint OffsetU14;

        public uint OffsetU15;
        public uint OffsetU16;
        public uint OffsetU17;
        public uint OffsetU18;

        public uint OffsetU19;

        public List<Patch> Patches = new List<Patch>();

        public void Load(string LoadPath)
        {
            using (Stream stream = File.Open(LoadPath, FileMode.Open))
            {
                Reader.SetDefaultReadMode(ByteOrder.LittleEndian);

                Magic = stream.ReadUInt32();
                U0 = stream.ReadUInt32();
                NumPatches = stream.ReadUInt32();
                NumU1 = stream.ReadUInt32();

                NumU2 = stream.ReadUInt32();
                NumU3 = stream.ReadUInt32();
                NumU4 = stream.ReadUInt32();
                NumU5 = stream.ReadUInt32();

                NumU6 = stream.ReadUInt32();
                NumU7 = stream.ReadUInt32();
                NumU8 = stream.ReadUInt32();
                NumU9 = stream.ReadUInt32();

                NumU10 = stream.ReadUInt32();
                NumU11 = stream.ReadUInt32();
                NumU12 = stream.ReadUInt32();
                NumU13 = stream.ReadUInt32();

                NumU14 = stream.ReadUInt32();
                NumU15 = stream.ReadUInt32();
                NumU16 = stream.ReadUInt32();
                NumU17 = stream.ReadUInt32();

                OffsetPatches = stream.ReadUInt32();
                OffsetPatchSourcePoints = stream.ReadUInt32();
                OffsetU1 = stream.ReadUInt32();
                OffsetU2 = stream.ReadUInt32();

                OffsetU3 = stream.ReadUInt32();
                OffsetU4 = stream.ReadUInt32();
                OffsetU5 = stream.ReadUInt32();
                OffsetU6 = stream.ReadUInt32();

                OffsetU7 = stream.ReadUInt32();
                OffsetU8 = stream.ReadUInt32();
                OffsetU9 = stream.ReadUInt32();
                OffsetU10 = stream.ReadUInt32();

                OffsetU11 = stream.ReadUInt32();
                OffsetU12 = stream.ReadUInt32();
                OffsetU13 = stream.ReadUInt32();
                OffsetU14 = stream.ReadUInt32();

                OffsetU15 = stream.ReadUInt32();
                OffsetU16 = stream.ReadUInt32();
                OffsetU17 = stream.ReadUInt32();
                OffsetU18 = stream.ReadUInt32();

                OffsetU19 = stream.ReadUInt32();

                stream.Position = OffsetPatches;
                Patches = new List<Patch>();
                for (int i = 0; i < NumPatches; i++)
                {
                    Patch patch = new Patch();

                    patch.UVPoint1 = stream.ReadVector4();
                    patch.UVPoint2 = stream.ReadVector4();
                    patch.UVPoint3 = stream.ReadVector4();
                    patch.UVPoint4 = stream.ReadVector4();

                    patch.LightMapPoint = stream.ReadVector4();

                    patch.R4C4 = stream.ReadVector4();
                    patch.R4C3 = stream.ReadVector4();
                    patch.R4C2 = stream.ReadVector4();
                    patch.R4C1 = stream.ReadVector4();

                    patch.R3C4 = stream.ReadVector4();
                    patch.R3C3 = stream.ReadVector4();
                    patch.R3C2 = stream.ReadVector4();
                    patch.R3C1 = stream.ReadVector4();

                    patch.R2C4 = stream.ReadVector4();
                    patch.R2C3 = stream.ReadVector4();
                    patch.R2C2 = stream.ReadVector4();
                    patch.R2C1 = stream.ReadVector4();

                    patch.R1C4 = stream.ReadVector4();
                    patch.R1C3 = stream.ReadVector4();
                    patch.R1C2 = stream.ReadVector4();
                    patch.R1C1 = stream.ReadVector4();

                    patch.LowestXYZ = stream.ReadVector3();
                    patch.HighestXYZ = stream.ReadVector3();

                    patch.U0 = stream.ReadUInt32();
                    patch.U1 = stream.ReadUInt32();
                    patch.U2 = stream.ReadUInt32();
                    patch.U3 = stream.ReadUInt32();
                    patch.U4 = stream.ReadUInt32();
                    patch.U5 = stream.ReadUInt32();

                    patch.Point1 = stream.ReadVector4();
                    patch.Point2 = stream.ReadVector4();
                    patch.Point3 = stream.ReadVector4();
                    patch.Point4 = stream.ReadVector4();

                    patch.U6 = stream.ReadUInt16();
                    patch.U7 = stream.ReadUInt16();
                    patch.U8 = stream.ReadUInt16();
                    patch.U9 = stream.ReadUInt16();

                    patch.U10 = new ushort[130];

                    for (int j = 0; j < 130; j++)
                    {
                        patch.U10[j] = stream.ReadUInt16();
                    }

                    patch.U11 = stream.ReadUInt16();

                    Patches.Add(patch);
                }
            }
        }
    }

    public struct Patch
    {
        public Vector4 UVPoint1;
        public Vector4 UVPoint2;
        public Vector4 UVPoint3;
        public Vector4 UVPoint4;

        public Vector4 LightMapPoint;

        public Vector4 R4C4;
        public Vector4 R4C3;
        public Vector4 R4C2;
        public Vector4 R4C1;
        public Vector4 R3C4;
        public Vector4 R3C3;
        public Vector4 R3C2;
        public Vector4 R3C1;
        public Vector4 R2C4;
        public Vector4 R2C3;
        public Vector4 R2C2;
        public Vector4 R2C1;
        public Vector4 R1C4;
        public Vector4 R1C3;
        public Vector4 R1C2;
        public Vector4 R1C1;

        public Vector3 LowestXYZ;
        public Vector3 HighestXYZ;

        public uint U0;
        public uint U1;
        public uint U2;
        public uint U3;
        public uint U4;
        public uint U5;

        public Vector4 Point1;
        public Vector4 Point2;
        public Vector4 Point3;
        public Vector4 Point4;

        public ushort U6;
        public ushort U7;
        public ushort U8;
        public ushort U9;
        public ushort[] U10; //130
        public ushort U11;
        //    u16 U6;
        //    u16 U7;
        //    u16 U8;
        //    u16 U9;
        //    u16 U10[130];
        //    u32 U12;
    }
}


//#pragma pattern_limit 2000000

//struct V4
//{
//    float X;
//    float Y;
//    float Z;
//    float W;
//};

//struct V3
//{
//    float X;
//    float Y;
//    float Z;
//};

//struct V2
//{
//    float X;
//    float Y;
//};

//struct Patch
//{
//    V4 UVPoint[4];
//    V4 Lightmap;
//    V4 Point[16];
//    V3 BBoxLow;
//    V3 BBoxHigh;
//    u32 U0;
//    u32 U1;
//    u32 U2;
//    u32 U3;
//    u32 U4;
//    u32 U5;
//    V4 CornerPoint[4];
//    u16 U6;
//    u16 U7;
//    u16 U8;
//    u16 U9;
//    u16 U10[130];
//    u32 U12;
//};

//struct PatchSourcePoint
//{
//    V3 Point;
//    V2 UV;
//};

//struct PointInfo
//{
//    V3 Position;
//    V3 Normal;
//    V2 UV;
//};

//struct Unknown2
//{
//    u32 Count;
//    u32 U1;
//    u32 U2;
//    PointInfo Points[Count];
//};

//struct Unknown3
//{
//    float Matrix[16];
//    u32 U1;
//    u32 U2;
//    u32 U3;
//    V3 BBoxLow;
//    V3 BBoxHigh;
//    u32 U4;
//    u32 U5;
//    u32 U6;
//    u32 U7;
//    u32 U8;
//    u32 U9;
//    u32 U10;
//    u32 U11;
//    float U12;
//    u32 U13;
//    u32 U14;
//};

//struct Unknown4
//{
//    float Matrix[16];
//    u32 U1;
//    V3 BBoxLow;
//    V3 BBoxHigh;
//    u32 U2;
//    u32 U3;
//    u32 U4;
//    u32 U5;
//    u32 U6;
//};

//struct Unknown5
//{
//    u16 U0;
//    u16 U1;
//    u32 U2;
//    u32 U3;
//    u32 U4;
//    u32 U5;
//    float U6;
//    u32 U7;
//    V4 U8;
//    u32 U9;
//    u32 U10;
//    u32 U11;
//    u32 U12;
//    u32 U13;
//    u32 U14;
//    u32 U15;
//};

//struct Unknown6
//{
//    u32 U1;
//    u32 U2[U1];
//};

//struct Unknown7
//{
//    u32 U1;
//    u32 U2;
//    float U3;
//    u32 U4;
//    V4 U5;
//    u32 U6;
//    u32 U7;
//    V3 U8;
//    V3 U9;
//    V3 U10;
//    u32 U11;
//    u32 U12;
//    float U13;
//    u32 U14;
//};

//struct Unknown8
//{
//    V3 U1;
//    V3 U2;
//    u32 U3;
//    u32 U4;
//    u32 U5;
//    u32 U6;
//};

//struct Unknown9
//{
//    u32 U1;
//    u32 U2;
//    u32 U3;
//    u32 U4;
//    u32 U5;
//    u32 U6;
//    u32 U7;
//    u32 U8;

//    V4 U9;
//    V4 U10;
//    V4 U11;
//    u32 U12;
//    u32 U13;
//    u32 U14;
//    V3 U15;
//    V3 U16;
//    float U17;
//    u32 U18;
//    u32 U19;
//};

//u32 Magic @ $;
//u32 NumPlayerStarts @ $;
//u32 NumPatches @ $;
//u32 NumUnknown1 @ $;
//u32 NumUnknown2 @ $;
//u32 NumUnknown3 @ $;
//u32 NumUnknown4 @ $;
//u32 NumUnknown5 @ $;
//u32 NumUnknown6 @ $;
//u32 NumUnknown7 @ $;
//u32 NumUnknown8 @ $;
//u32 NumUnknown9 @ $;
//u32 NumUnknown10 @ $;
//u32 NumUnknown11 @ $;
//u32 NumUnknown12 @ $;
//u32 NumUnknown13 @ $;
//u32 NumUnknown14 @ $;
//u32 NumUnknown15 @ $;
//u32 NumUnknown16 @ $;
//u32 NumUnknown17 @ $;

//u32 OffsetPatches @ $;
//u32 OffsetPatchSourcePoints @ $;
//u32 OffsetUnknown2 @ $;
//u32 OffsetUnknown3 @ $;
//u32 OffsetUnknown4 @ $;
//u32 OffsetUnknown5 @ $;
//u32 OffsetUnknown6 @ $;
//u32 OffsetUnknown7 @ $;
//u32 OffsetUnknown8 @ $;
//u32 OffsetUnknown9 @ $;
//u32 OffsetUnknown10 @ $;
//u32 OffsetUnknown11 @ $;
//u32 OffsetUnknown12 @ $;
//u32 OffsetUnknown13 @ $;
//u32 OffsetUnknown14 @ $;
//u32 OffsetUnknown15 @ $;
//u32 OffsetUnknown16 @ $;
//u32 OffsetUnknown17 @ $;
//u32 OffsetUnknown18 @ $;
//u32 OffsetUnknown19 @ $;

//Patch Patches[NumPatches] @ OffsetPatches;
//PatchSourcePoint PatchSourcePoints[NumPatches * 16] @ OffsetPatchSourcePoints;
//Unknown2 U2 @ OffsetUnknown2;
//Unknown3 U3[NumUnknown3] @ OffsetUnknown3;
//Unknown4 U4[NumUnknown4] @ OffsetUnknown4;
//Unknown5 U5[NumUnknown5] @ OffsetUnknown5;
//Unknown6 U6[NumUnknown6] @ OffsetUnknown6;
//Unknown7 U7[NumUnknown7] @ OffsetUnknown7;
//Unknown8 U8[NumUnknown8] @ OffsetUnknown8;
//Unknown9 U9[NumUnknown9] @ OffsetUnknown9;