using SSXLibrary.JsonFiles.SSX3;
using SSX_Library.Internal.Utilities;
using System.Collections.Generic;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData
{
    public class WorldInstance
    {
        // The instance tail carries a per-model-vertex baked-lighting array
        // (one 16-bit ABGR1555 colour per vertex, potentially thousands). Gate
        // its JSON emission behind this flag so normal exports stay small.
        public static bool EmitVertexColors = false;

        public string Name;

        public ObjectID objectID;

        public int U0;
        public int U1;
        public int U2;
        public int U3;

        public Matrix4x4 matrix4X4 = new Matrix4x4();
        public Vector4 V0;
        public Vector3 V1;
        public Vector3 V2;

        public int U4;

        public ObjectID ModelID;

        public float U5;
        public int U6;
        public int U7;

        public int U8;
        public int U9;
        public int U10;
        public int U11;
        public int U12;

        // Per-model-vertex baked lighting, one raw 16-bit ABGR1555 colour per
        // model vertex in PS2 vertex-stream order (a=bit15, b=10-14, g=5-9,
        // r=0-4). Concatenated across the model's parts (multiple UNPACK blocks).
        public List<int> VertexColors = new List<int>();

        public void LoadData(Stream stream)
        {
            U0 = StreamUtil.ReadInt32(stream);
            U1 = StreamUtil.ReadInt32(stream);
            U2 = StreamUtil.ReadInt32(stream);
            U3 = StreamUtil.ReadInt32(stream);

            matrix4X4 = StreamUtil.ReadMatrix4x4(stream);
            V0 = StreamUtil.ReadVector4(stream);
            V1 = StreamUtil.ReadVector3(stream);
            V2 = StreamUtil.ReadVector3(stream);

            objectID = WorldCommon.ObjectIDLoad(stream); 
            U4 = StreamUtil.ReadInt32(stream);

            ModelID = WorldCommon.ObjectIDLoad(stream);

            U5 = StreamUtil.ReadFloat(stream);
            U6 = StreamUtil.ReadInt32(stream);
            U7 = StreamUtil.ReadInt32(stream);

            U8 = StreamUtil.ReadInt16(stream);
            U9 = StreamUtil.ReadInt16(stream);
            U10 = StreamUtil.ReadInt32(stream);
            U11 = StreamUtil.ReadInt32(stream);
            U12 = StreamUtil.ReadInt32(stream);

            // MODEL DATA (instance tail): a PS2 DMA/VIF chain that uploads this
            // instance's per-vertex baked lighting. The chunk substream is exactly
            // ChunkSize bytes (SSBHandler slices NewData into memoryStream1), so
            // stream.Length bounds the tail.
            //
            // We only need the colour payload. It is carried by VIF UNPACK V4-5
            // opcodes (command byte 0x6F, or 0x7F with the mask flag): NUM in bits
            // 16-23, followed by NUM little-endian 16-bit ABGR1555 colours (one per
            // model vertex). A multi-part model emits one UNPACK per part; we
            // concatenate them in order. Everything else in the tail -- the leading
            // 0x30-tag DMA records, the transfer preamble, and each part's
            // 0x20000000/0/0xDEADBEEF/0x04000001 footer -- is skipped word by word.
            // This is safe: no DMA-tag, preamble, or footer word has top byte
            // 0x6F/0x7F, and the colour halfwords are consumed inside the inner loop
            // (never rescanned as opcodes). Short/odd framing just stops the scan,
            // leaving the header parse intact.
            long tailEnd = stream.Length;
            while (stream.Position + 4 <= tailEnd)
            {
                uint w = (uint)StreamUtil.ReadUInt32(stream);
                int cmd = (int)((w >> 24) & 0xFF);
                if (cmd == 0x6F || cmd == 0x7F) // VIF UNPACK V4-5 (16-bit RGBA)
                {
                    int num = (int)((w >> 16) & 0xFF);
                    if (num == 0) num = 256;
                    for (int i = 0; i < num && stream.Position + 2 <= tailEnd; i++)
                    {
                        VertexColors.Add(StreamUtil.ReadUInt16(stream)); // raw ABGR1555
                    }
                    // Realign to the next 32-bit word boundary after the colours.
                    if ((stream.Position & 3) != 0)
                    {
                        stream.Position += 4 - (stream.Position & 3);
                    }
                }
            }
        }

        public InstanceJsonHandler.Instance ToJSON()
        {
            InstanceJsonHandler.Instance bin3File = new InstanceJsonHandler.Instance();

            bin3File.Name = Name;

            bin3File.U0 = U0;
            bin3File.U1 = U1;
            bin3File.U2 = U2;
            bin3File.U3 = U3;

            Vector3 Scale;
            Quaternion Rotation;
            Vector3 Location;

            Matrix4x4.Decompose(matrix4X4, out Scale, out Rotation, out Location);
            bin3File.Position = ArrayConv.Vector3ToArray(Location);
            bin3File.Rotation = ArrayConv.QuaternionToArray(Rotation);
            bin3File.Scale = ArrayConv.Vector3ToArray(Scale);

            bin3File.V0 = ArrayConv.Vector4ToArray(V0);
            bin3File.V1 = ArrayConv.Vector3ToArray(V1);
            bin3File.V2 = ArrayConv.Vector3ToArray(V2);

            bin3File.TrackID = objectID.TrackID;
            bin3File.RID = objectID.RID;
            bin3File.U4 = U4;

            bin3File.ModelTrackID = ModelID.TrackID;
            bin3File.ModelRID = ModelID.RID;
            bin3File.U5 = U5;
            bin3File.U6 = U6;
            bin3File.U7 = U7;

            bin3File.U8 = U8;
            bin3File.U9 = U9;
            bin3File.U10 = U10;
            bin3File.U11 = U11;
            bin3File.U12 = U12;

            // Large per-vertex bake array — only emit when explicitly requested.
            if (EmitVertexColors)
            {
                bin3File.VertexColors = VertexColors;
            }

            return bin3File;
        }
    }
}
