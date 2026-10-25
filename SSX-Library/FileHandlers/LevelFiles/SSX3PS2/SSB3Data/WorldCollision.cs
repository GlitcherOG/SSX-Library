using SSXLibrary.Internal.Utilities;
using System.Globalization;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data
{
    public class WorldCollision
    {
        public ObjectID objectID;

        public int ColliderType; //1 - Mesh, 3 - Sphere
        public int Count;

        public int ModelsOffset;
        public int DeadBeefOffset;
        public int DeadBeef1Offset;

        public List<MeshCollider> Models = new List<MeshCollider>();


        public void LoadData(Stream stream, int TrackID, int RID)
        {
            objectID.TrackID = TrackID;
            objectID.RID = RID;

            ColliderType = StreamUtil.ReadInt16(stream);
            Count = StreamUtil.ReadInt16(stream);

            ModelsOffset = StreamUtil.ReadInt32(stream);
            DeadBeefOffset = StreamUtil.ReadInt32(stream);
            DeadBeef1Offset = StreamUtil.ReadInt32(stream);

            if (ColliderType == 1)
            {
                Models = new List<MeshCollider>();
                for (int i = 0; i < Count; i++)
                {
                    long Pos = stream.Position;

                    var TempU0Struct = new MeshCollider();

                    TempU0Struct.IndicesCount = StreamUtil.ReadInt16(stream);
                    TempU0Struct.VectorsCount = StreamUtil.ReadInt16(stream);

                    TempU0Struct.IndicesOffset = StreamUtil.ReadInt32(stream);
                    TempU0Struct.BBoxOffset = StreamUtil.ReadInt32(stream);
                    TempU0Struct.VectorsOffset = StreamUtil.ReadInt32(stream);
                    TempU0Struct.NormalOffset = StreamUtil.ReadInt32(stream);

                    TempU0Struct.Indices = new List<Indexs>();

                    stream.Position = Pos + TempU0Struct.IndicesOffset;
                    for (int j = 0; j < TempU0Struct.IndicesCount; j++)
                    {
                        var TempIndex = new Indexs();

                        TempIndex.Index1 = StreamUtil.ReadInt8(stream);
                        TempIndex.Index2 = StreamUtil.ReadInt8(stream);
                        TempIndex.Index3 = StreamUtil.ReadInt8(stream);

                        TempU0Struct.Indices.Add(TempIndex);
                    }

                    //MathBbox Info
                    stream.Position = Pos + TempU0Struct.BBoxOffset;
                    TempU0Struct.Bboxes = new List<BBox>();
                    int BBoxCount = (TempU0Struct.VectorsOffset - TempU0Struct.BBoxOffset) / 24;
                    for (int j = 0; j < BBoxCount; j++)
                    {
                        var BBox = new BBox();

                        BBox.BBoxLow = StreamUtil.ReadVector3(stream);
                        BBox.BBoxHigh = StreamUtil.ReadVector3(stream);

                        TempU0Struct.Bboxes.Add(BBox);
                    }

                    stream.Position = Pos + TempU0Struct.VectorsOffset;
                    TempU0Struct.Vectors = new List<Vector4>();

                    for (int j = 0; j < TempU0Struct.VectorsCount; j++)
                    {
                        TempU0Struct.Vectors.Add(StreamUtil.ReadVector4(stream));
                    }

                    stream.Position = Pos + TempU0Struct.NormalOffset;
                    TempU0Struct.Normals = new List<Vector4>();

                    for (int j = 0; j < TempU0Struct.IndicesCount; j++)
                    {
                        TempU0Struct.Normals.Add(StreamUtil.ReadVector4(stream));
                    }

                    Models.Add(TempU0Struct);
                }
            }
            else if (ColliderType==3)
            {

            }
            else
            {
                Console.WriteLine("Unknown Collider Type " + ColliderType);
            }
        }

        public void CollisionObjectSave(string Path, string Name)
        {
            string output = "# Exported From SSX Using SSX Multitool Modder by GlitcherOG \n";

            //Combine Lists
            List<Vector4> Vertices = new List<Vector4>();
            List<Vector4> Normals = new List<Vector4>();
            List<Indexs> indices = new List<Indexs>();
            List<int> ModelIndicesCount = new List<int>();
            List<int> VerticesCount = new List<int>();
            int StartPointI = 0;
            int StartPointV = 0;
            for (int i = 0; i < Models.Count; i++)
            {
                ModelIndicesCount.Add(StartPointI);
                VerticesCount.Add(StartPointV);

                Vertices.AddRange(Models[i].Vectors);
                Normals.AddRange(Models[i].Normals);

                for (global::System.Int32 j = 0; j < Models[i].Indices.Count; j++)
                {
                    var TempIndices = new Indexs();

                    TempIndices.Index1 = Models[i].Indices[j].Index1 + StartPointV;
                    TempIndices.Index2 = Models[i].Indices[j].Index2 + StartPointV;
                    TempIndices.Index3 = Models[i].Indices[j].Index3 + StartPointV;

                    indices.Add(TempIndices);   
                }

                StartPointI += Models[i].Indices.Count;
                StartPointV += Models[i].Vectors.Count;
            }


            for (int i = 0; i < Vertices.Count; i++)
            {
                output += "v " + Vertices[i].X.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Vertices[i].Y.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Vertices[i].Z.ToString(CultureInfo.InvariantCulture.NumberFormat) + "\n";
            }

            for (int i = 0; i < Normals.Count; i++)
            {
                output += "vn " + Normals[i].X.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Normals[i].Y.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Normals[i].Z.ToString(CultureInfo.InvariantCulture.NumberFormat) + "\n";
            }


            for (int i = 0; i < indices.Count; i++)
            {
                if(ModelIndicesCount.Contains(i))
                {
                    output += "o Mesh" + ModelIndicesCount.IndexOf(i).ToString() + "\n";
                }

                output += "f " + (indices[i].Index1+1).ToString() + "//" + (i + 1).ToString() + " " + (indices[i].Index2 + 1).ToString() + "//" + (i + 1).ToString() + " " + (indices[i].Index3 + 1).ToString() + "//" + (i + 1).ToString() + "\n";
            }

            File.WriteAllText(Path + "/" + objectID.RID.ToString() + "-" + Name + ".obj", output);
        }

        public struct MeshCollider
        { 
            public int IndicesCount;
            public int VectorsCount;

            public int IndicesOffset;
            public int BBoxOffset;
            public int VectorsOffset;
            public int NormalOffset;

            public List<Indexs> Indices;
            public List<BBox> Bboxes;
            public List<Vector4> Vectors;
            public List<Vector4> Normals;
        }

        public struct Indexs
        {
            public int Index1;
            public int Index2;
            public int Index3;
        }

        public struct BBox
        {
            public Vector3 BBoxLow;
            public Vector3 BBoxHigh;
        }

        public struct SphereCollider
        {
            /*
            #pragma description SSX 3 world resource 12, type 3: physics bodies with sphere trees (e.g. *_CollideModel_SphereTree)
#pragma endian little

import std.mem;
import std.core;

struct Vec3 {
    float x;
    float y;
    float z;
};

struct Header {
    u16 type;            // 3 = sphere tree bodies (type 1 is the static mesh collision WorldCollision reads)
    u16 bodyCount;       // one per fragment / physics body
    u32 firstBody;       // 0x10
    u32 runtimeHeader;   // 8 bytes of DEADBEEF straight after the last body (filled in at load)
    u32 runtimeBodies;   // next 16 byte boundary after runtimeHeader + 8, bodyCount * 320 bytes of DEADBEEF
};

struct SphereLevel {
    u32 sphereCount;       // 1, 8, 64, 512 (8^level, the spheres sit on an implicit octree grid)
    float radius;
    float radiusOverRoot2; // always radius / sqrt(2)
};

struct TreeToken {
    s8 control;          // -(number of mask bytes)
    u8 masks[-control];  // octant bitmasks, one bit per child (0F/F0 = z halves, 33/CC = y, 55/AA = x)
    u16 flags;           // seen 2-8, 16+n, 32+n, 64+n; 0x7E = end of tree (bitfield, not decoded)
};

// Start of the 0x007E end markers, found by walking back from the 5 byte root tail
fn end_marker_start(u32 treeStart, u32 tailStart) {
    u32 pos = tailStart;
    while (pos >= treeStart + 2 && std::mem::read_unsigned(pos - 2, 2) == 0x7E) {
        pos = pos - 2;
    }
    return pos;
};

struct Body {
    u32 group;           // 1-4, not the fragment shape (material / sound / layer?)
    u32 treeSize;
    u32 unknown1;        // always 1
    u32 depth;           // 4, or 1 for a single sphere
    Vec3 origin;         // ~0 for fragments, equals centreOfMass on single spheres
    Vec3 centreOfMass;
    float inertia[9];        // 3x3 symmetric inertia tensor
    float inverseInertia[9]; // its inverse (checked on every body)
    float boundingRadius;    // matches the fragment mesh radius
    u32 unknown2;            // 0
    SphereLevel levels[depth];
    u32 leafCount;           // 8^depth

    // Tree stream: [optional 2 byte prefix] tokens, 0x007E end markers, 5 byte root tail
    u32 treeStart = $;
    u32 treeEnd = treeStart + treeSize;
    u32 tailStart = treeEnd - 5;
    u32 runStart = end_marker_start(treeStart, tailStart);

    if (treeSize > 5 && std::mem::read_signed(treeStart, 1) >= 0) {
        u8 treePrefix;       // only 2 bodies have this (04), the rest start straight into tokens
        u8 prefixRootMask;   // FF
    }
    TreeToken tokens[while($ < runStart && std::mem::read_signed($, 1) < 0)];
    if ($ < tailStart) {
        u16 endMarkers[(tailStart - $) / 2];  // all 0x007E (31 of them after a depth 4 tree)
    }
    u16 tailValue;       // 7 on depth 1, 0x21-0x2D on depth 4
    u8 rootMask;         // FF
    u16 tailZero;        // 0

    padding[((treeEnd + 4) & ~3) - treeEnd];  // 1-4 bytes of leftover memory (seen ".tga")
};

struct RuntimeBody {
    u32 words[80];       // 320 bytes of DEADBEEF, runtime state per body
};

Header header @ 0x00;

Body bodies[header.bodyCount] @ header.firstBody;
u32 runtimeHeaderWords[2] @ header.runtimeHeader;
RuntimeBody runtimeBodies[header.bodyCount] @ header.runtimeBodies;
            */
        }
    }
}
