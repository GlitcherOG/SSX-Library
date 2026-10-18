using ICSharpCode.SharpZipLib.Core;
using SSXLibrary.Internal.Utilities;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SSXLibrary.JsonFiles.SSX3;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data
{
    public class WorldBin4
    {
        public ObjectID objectID;

        public int ModelCount; //Count?
        public int ModelOffset; //Offset?
        public int U2;

        public int U3;
        public int U4;
        public int U5;
        public int U6;

        public int U7; //Offset Unknown
        public int PointDataHeaderOffset; //Offset Vertex?
        public int U9; //Unknown Offset
        public int U10; //Unknown Offset

        //Bbox Info
        public Vector3 BboxLow;
        public Vector3 BboxHigh;

        public int U13;
        public int PointDataCount;
        public int PointDataOffset;

        public List<PointData> PointDataList = new List<PointData>();

        public void LoadData(Stream stream)
        {
            objectID = WorldCommon.ObjectIDLoad(stream);

            ModelCount = StreamUtil.ReadUInt32(stream);
            ModelOffset = StreamUtil.ReadUInt32(stream);
            U2 = StreamUtil.ReadUInt32(stream);

            U3 = StreamUtil.ReadUInt32(stream);
            U4 = StreamUtil.ReadUInt32(stream);
            U5 = StreamUtil.ReadUInt32(stream);
            U6 = StreamUtil.ReadUInt32(stream);

            if(ModelCount!=1||ModelOffset!=32)
            {
                Console.WriteLine("Error World Bin 4");
            }

            U7 = StreamUtil.ReadUInt32(stream);
            PointDataHeaderOffset = StreamUtil.ReadUInt32(stream);
            U9 = StreamUtil.ReadUInt32(stream);
            U10 = StreamUtil.ReadUInt32(stream);

            if(PointDataHeaderOffset!=48)
            {
                Console.WriteLine("Error World Bin 4");
            }

            BboxLow = StreamUtil.ReadVector3(stream);
            BboxHigh = StreamUtil.ReadVector3(stream);

            U13 = StreamUtil.ReadUInt32(stream);
            PointDataCount = StreamUtil.ReadUInt32(stream);
            PointDataOffset = StreamUtil.ReadUInt32(stream);

            PointDataList = new List<PointData>();

            for (int i = 0; i < PointDataCount; i++)
            {
                PointData pointData = new PointData();

                pointData.UVector1 = StreamUtil.ReadVector3(stream);
                pointData.UVector2 = StreamUtil.ReadVector3(stream);
                pointData.U0 = StreamUtil.ReadFloat(stream);

                PointDataList.Add(pointData);
            }
        }

        public Bin4JsonHandler.Bin4 ToJSON()
        {
            Bin4JsonHandler.Bin4 bin4 = new Bin4JsonHandler.Bin4();
            bin4.Name = objectID.RID.ToString();

            bin4.objectID = objectID;

            bin4.PointDatas = new List<Bin4JsonHandler.PointData>();
            for (int i = 0; i < PointDataList.Count; i++)
            {
                var TempPointData = new Bin4JsonHandler.PointData();

                TempPointData.UVector1 = ArrayConv.Vector3ToArray(PointDataList[i].UVector1);
                TempPointData.UVector2 = ArrayConv.Vector3ToArray(PointDataList[i].UVector2);

                TempPointData.U0 = PointDataList[i].U0;

                bin4.PointDatas.Add(TempPointData);
            }

            return bin4;
        }



        //the general structrue of this is vector 3, vector 3, float

        public struct PointData
        {
            public Vector3 UVector1;
            public Vector3 UVector2;

            public float U0;
        }

    }
}
