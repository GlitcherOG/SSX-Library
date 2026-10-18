using SSXLibrary.Internal.Utilities;
using SSXLibrary.JsonFiles.SSX3;
using SSXLibrary.Internal.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data
{
    public class WorldPatch
    {
        public ObjectID objectID;

        public string Name = "Null";

        public int U0;
        public int U1;
        public int U2;
        public int U3;
        public int U4;
        public int U5;

        public Vector4 Lightmap;
        public Vector2[] UV = new Vector2[4];

        public Vector4[] controlPoints = new Vector4[16];

        public Vector4 U7;

        public int U10;
        public int StreamingChunkID;

        public Vector3 Point1;
        public Vector3 Point2;
        public Vector3 Point3;
        public Vector3 Point4;

        public Vector3 BBoxMin;
        public Vector3 BBoxMax;

        public int TextureRID;
        public int LightmapRID;
        public int U14;
        public int U15;
        public int U16;
        public int U17;
        public int U18;
        public int U19;

        public void LoadPatch(Stream stream)
        {
            U0 = StreamUtil.ReadInt32(stream);
            U1 = StreamUtil.ReadInt32(stream);
            U2 = StreamUtil.ReadInt16(stream);
            U3 = StreamUtil.ReadInt16(stream);
            U4 = StreamUtil.ReadInt16(stream);
            U5 = StreamUtil.ReadInt16(stream);

            Lightmap = StreamUtil.ReadVector4(stream);

            UV = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                UV[i] = StreamUtil.ReadVector2(stream);
            }

            controlPoints = new Vector4[16];
            for (int i = 0; i < 16; i++)
            {
                controlPoints[i] = StreamUtil.ReadVector4(stream);
            }

            U7 = StreamUtil.ReadVector4(stream);
            objectID = WorldCommon.ObjectIDLoad(stream);
            U10 = StreamUtil.ReadInt16(stream);
            StreamingChunkID = StreamUtil.ReadInt16(stream);

            Point1 = StreamUtil.ReadVector3(stream);
            Point2 = StreamUtil.ReadVector3(stream);
            Point3 = StreamUtil.ReadVector3(stream);
            Point4 = StreamUtil.ReadVector3(stream);

            BBoxMin = StreamUtil.ReadVector3(stream);
            BBoxMax = StreamUtil.ReadVector3(stream);

            TextureRID = StreamUtil.ReadInt16(stream);
            LightmapRID = StreamUtil.ReadInt16(stream);
            U14 = StreamUtil.ReadInt16(stream);
            U15 = StreamUtil.ReadInt16(stream);
            U16 = StreamUtil.ReadInt16(stream);
            U17 = StreamUtil.ReadInt16(stream);
            U18 = StreamUtil.ReadInt16(stream);
            U19 = StreamUtil.ReadInt16(stream);
        }

        public PatchesJsonHandler.PatchJson ToJSON()
        {
            PatchesJsonHandler.PatchJson patchJson = new PatchesJsonHandler.PatchJson();

            patchJson.Name = Name;

            patchJson.U1 = U1;
            patchJson.U2 = U2;
            patchJson.U3 = U3;
            patchJson.U4 = U4;
            patchJson.U5 = U5;

            //patchJson.TexturePath = "0000.png";

            patchJson.LightMapPoint = ArrayConv.Vector4ToArray(Lightmap);
            patchJson.UVPoints = new float[4, 2];

            for (int i = 0; i < 4; i++)
            {
                patchJson.UVPoints[i, 0] = UV[i].X;
                patchJson.UVPoints[i, 1] = UV[i].Y;
            }

            BezierUtil bezierUtil = new BezierUtil();
            for (int i = 0; i < 16; i++)
            {
                bezierUtil.ProcessedPoints[i] = VectorConv.Vector4ToVector3(controlPoints[15-i]);
            }

            bezierUtil.GenerateRawPoints();

            patchJson.Points = new float[16, 3];

            for (int a = 0; a < 16; a++)
            {
                patchJson.Points = ArrayConv.Vector3ToArray2D(patchJson.Points, bezierUtil.RawPoints[a], a);
            }

            patchJson.U7 = ArrayConv.Vector4ToArray(U7);

            patchJson.TrackID = objectID.TrackID;
            patchJson.RID = objectID.RID;

            patchJson.U10 = U10;
            patchJson.TextureRID = TextureRID;
            patchJson.LightmapRID = LightmapRID;
            patchJson.U14 = U14;
            patchJson.U15 = U15;
            patchJson.U16 = U16;
            patchJson.U17 = U17;
            patchJson.U18 = U18;
            patchJson.U19 = U19;

            return patchJson;
        }
    }
}
