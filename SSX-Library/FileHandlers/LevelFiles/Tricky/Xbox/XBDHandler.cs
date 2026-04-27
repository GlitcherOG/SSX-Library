using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.FileHandlers.LevelFiles.Tricky.Xbox
{
    public class XBDHandler
    {


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