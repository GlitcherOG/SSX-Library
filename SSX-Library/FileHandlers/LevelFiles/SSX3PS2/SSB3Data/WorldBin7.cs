using SSXLibrary.Internal.Utilities;
using SSXLibrary.Internal.Utilities.StreamExtensions;
using SSXLibrary.JsonFiles.SSX3;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data
{
    public class WorldBin7
    {
        public int RID;
        public int TrackID;

        public int U0;
        public int U1;
        public int U2;
        public int U3;

        public Vector3 U4;
        public Vector3 U5;
        public Vector3 U6;
        public Vector3 U7;

        public int U8;
        public int U9;
        public int U10;
        public int U11;

        public void LoadData(Stream stream, int trackID, int rID)
        {
            if(stream.Length!=0x50)
            {
                Console.WriteLine("Error");
            }

            TrackID = trackID;
            RID = rID;

            U0 = StreamUtil.ReadUInt32(stream);
            U1 = StreamUtil.ReadUInt32(stream);
            U2 = StreamUtil.ReadUInt32(stream);
            U3 = StreamUtil.ReadUInt32(stream);

            U4 = StreamUtil.ReadVector3(stream);
            U5 = StreamUtil.ReadVector3(stream);
            U6 = StreamUtil.ReadVector3(stream);
            U7 = StreamUtil.ReadVector3(stream);

            U8 = StreamUtil.ReadUInt32(stream);
            U9 = StreamUtil.ReadUInt32(stream);
            U10 = StreamUtil.ReadUInt32(stream);
            U11 = StreamUtil.ReadUInt32(stream);
        }

        public Bin7JsonHandler.Bin7File ToJSON()
        {
            Bin7JsonHandler.Bin7File bin7File = new Bin7JsonHandler.Bin7File();

            bin7File.TrackID = TrackID;
            bin7File.RID = RID;

            bin7File.U0 = U0;
            bin7File.U1 = U1;
            bin7File.U2 = U2;
            bin7File.U3 = U3;

            bin7File.U4 = ArrayConv.Vector3ToArray(U4);
            bin7File.U5 = ArrayConv.Vector3ToArray(U5);
            bin7File.U6 = ArrayConv.Vector3ToArray(U6);
            bin7File.U7 = ArrayConv.Vector3ToArray(U7);

            bin7File.U8 = U8;
            bin7File.U9 = U9;
            bin7File.U10 = U10;
            bin7File.U11 = U11;

            return bin7File;
        }
    }
}
