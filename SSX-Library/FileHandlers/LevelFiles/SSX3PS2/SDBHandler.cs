using SSX_Library.Internal.Utilities;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2
{
    public class SDBHandler
    {
        public byte[] UnknownBytes = new byte[4];
        public float UnknownFloat;
        public int numLocations; //4
        public int numChunks; //4
        public int numStreamingChunks; //4
        public byte[] UnknownBytes2 = new byte[60];

        public List<Location> locations = new List<Location>();
        public List<ChunkInfo> chunksInfo = new List<ChunkInfo>();
        public List<StreamingChunkInfo> streamingChunkInfos = new List<StreamingChunkInfo>();

        public void LoadSBD(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                UnknownBytes = StreamUtil.ReadBytes(stream, 4);
                UnknownFloat = StreamUtil.ReadFloat(stream);
                numLocations = StreamUtil.ReadUInt32(stream);
                numChunks = StreamUtil.ReadUInt32(stream);
                numStreamingChunks = StreamUtil.ReadUInt32(stream);
                UnknownBytes2 = StreamUtil.ReadBytes(stream, 60);

                int TempData = 0;

                locations = new List<Location>();
                for (int i = 0; i < numLocations; i++)
                {
                    var TempLocation = new Location();
                    TempLocation.Name = StreamUtil.ReadString(stream, 16);
                    TempLocation.numStreamingChunks = StreamUtil.ReadUInt32(stream);
                    TempLocation.numChunks = StreamUtil.ReadUInt32(stream);
                    TempLocation.posEndStreamingChunk = StreamUtil.ReadUInt32(stream);
                    TempLocation.posChunks = StreamUtil.ReadUInt32(stream);

                    TempLocation.numMaterials = StreamUtil.ReadInt16(stream);
                    TempLocation.numPatches = StreamUtil.ReadInt16(stream);
                    TempLocation.numWorldMDR = StreamUtil.ReadInt16(stream);
                    TempLocation.numInstance = StreamUtil.ReadInt16(stream);
                    TempLocation.numParticleModel = StreamUtil.ReadInt16(stream);
                    TempLocation.numParticleInstance = StreamUtil.ReadInt16(stream);
                    TempLocation.numLights = StreamUtil.ReadInt16(stream);
                    TempLocation.numHalo = StreamUtil.ReadInt16(stream);
                    TempLocation.numSplines = StreamUtil.ReadInt16(stream);
                    TempLocation.numShape = StreamUtil.ReadInt16(stream);
                    TempLocation.numShapelightmap = StreamUtil.ReadInt16(stream);
                    TempLocation.numVisCurtains = StreamUtil.ReadInt16(stream);
                    TempLocation.numCollision = StreamUtil.ReadInt16(stream);
                    TempLocation.numSoundTrigger = StreamUtil.ReadInt16(stream);
                    TempLocation.numAIP = StreamUtil.ReadInt16(stream);
                    TempLocation.numWorldPainter = StreamUtil.ReadInt16(stream);
                    TempLocation.numScripts = StreamUtil.ReadInt16(stream);
                    TempLocation.numCameraTrigger = StreamUtil.ReadInt16(stream);
                    TempLocation.numNISTable = StreamUtil.ReadInt16(stream);
                    TempLocation.numMissions = StreamUtil.ReadInt16(stream);
                    TempLocation.numAudioBanks = StreamUtil.ReadInt16(stream);
                    TempLocation.numRadar = StreamUtil.ReadInt16(stream);
                    TempLocation.numAvalancheAnimation = StreamUtil.ReadInt16(stream);
                    TempLocation.U1 = StreamUtil.ReadInt16(stream);
                    TempLocation.U2 = StreamUtil.ReadInt16(stream);
                    TempLocation.U3 = StreamUtil.ReadInt16(stream);
                    TempLocation.U4 = StreamUtil.ReadInt16(stream);
                    TempLocation.U5 = StreamUtil.ReadInt16(stream);

                    locations.Add(TempLocation);
                }

                StreamUtil.AlignBy16(stream);
                chunksInfo = new List<ChunkInfo>();
                for (int i = 0; i < numChunks; i++)
                {
                    var TempUnknown1 = new ChunkInfo();
                    TempUnknown1.BboxLow = StreamUtil.ReadVector4(stream);
                    TempUnknown1.BboxHigh = StreamUtil.ReadVector4(stream);
                    TempUnknown1.UnknownFloat9 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat10 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat11 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat12 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat13 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat14 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat15 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat16 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat17 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat18 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat19 = StreamUtil.ReadFloat(stream);
                    TempUnknown1.UnknownFloat20 = StreamUtil.ReadFloat(stream);

                    TempUnknown1.UnknownInt1 = StreamUtil.ReadUInt32(stream);
                    TempUnknown1.UnknownInt2 = StreamUtil.ReadUInt32(stream);
                    TempUnknown1.UnknownInt3 = StreamUtil.ReadUInt32(stream);
                    TempUnknown1.UnknownInt4 = StreamUtil.ReadUInt32(stream);

                    chunksInfo.Add(TempUnknown1);
                }

                streamingChunkInfos = new List<StreamingChunkInfo>();

                for (int i = 0; i < numStreamingChunks; i++)
                {
                    var TempUnknown2 = new StreamingChunkInfo();
                    TempUnknown2.numResources = StreamUtil.ReadUInt16(stream);
                    TempUnknown2.subChunkID = StreamUtil.ReadUInt24(stream);
                    TempUnknown2.chunkOffset = StreamUtil.ReadUInt24(stream);
                    TempUnknown2.unpackedSize = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.numMaterials = StreamUtil.ReadUInt16(stream); //0
                    TempUnknown2.numPatches = StreamUtil.ReadUInt16(stream); //1
                    TempUnknown2.numWorldMDR = StreamUtil.ReadUInt16(stream); //WorldMDR Count - ID 2
                    TempUnknown2.numInstance = StreamUtil.ReadUInt16(stream); //3
                    TempUnknown2.numParticleModel = StreamUtil.ReadUInt16(stream); //4
                    TempUnknown2.numParticleInstance = StreamUtil.ReadUInt16(stream); //5
                    TempUnknown2.numParticleInstance = StreamUtil.ReadUInt16(stream); //6
                    TempUnknown2.numLights = StreamUtil.ReadUInt16(stream); //7
                    TempUnknown2.numHalo = StreamUtil.ReadUInt16(stream); //8
                    TempUnknown2.numShapes = StreamUtil.ReadUInt16(stream); //Shape Count - ID 9
                    TempUnknown2.numShapeLightmap = StreamUtil.ReadUInt16(stream); //10
                    TempUnknown2.numVisCurtains = StreamUtil.ReadUInt16(stream); //11
                    TempUnknown2.numCollision = StreamUtil.ReadUInt16(stream); //12

                    TempUnknown2.U1 = StreamUtil.ReadUInt16(stream);
                    TempUnknown2.U2 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U3 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U4 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U5 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U6 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U7 = StreamUtil.ReadUInt32(stream);
                    TempUnknown2.U8 = StreamUtil.ReadUInt32(stream);
                    streamingChunkInfos.Add(TempUnknown2);
                }
            }

        }

        public void Save(string path)
        {
            //using (Stream stream = File.Open(path, FileMode.Open))
            //{
            //    UnknownBytes = StreamUtil.ReadBytes(stream, 4);
            //    UnknownFloat = StreamUtil.ReadFloat(stream);
            //    numLocations = StreamUtil.ReadUInt32(stream);
            //    numChunks = StreamUtil.ReadUInt32(stream);
            //    numUnknown2 = StreamUtil.ReadUInt32(stream);
            //    UnknownBytes2 = StreamUtil.ReadBytes(stream, 60);

            //    locations = new List<Location>();
            //    for (int i = 0; i < numLocations; i++)
            //    {
            //        var TempLocation = new Location();
            //        TempLocation.Name = StreamUtil.ReadString(stream, 16);
            //        TempLocation.numUnknown2 = StreamUtil.ReadUInt32(stream);
            //        TempLocation.numChunks = StreamUtil.ReadUInt32(stream);
            //        TempLocation.posChunks = StreamUtil.ReadUInt32(stream);
            //        TempLocation.posUnknown2 = StreamUtil.ReadUInt32(stream);

            //        TempLocation.Unknown5 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown6 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown7 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown8 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown9 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown10 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown11 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown12 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown13 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown14 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown15 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown16 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown17 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown18 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown19 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown20 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown21 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown22 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown23 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown24 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown25 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown26 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown27 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown28 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown29 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown30 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown31 = StreamUtil.ReadInt16(stream);
            //        TempLocation.Unknown32 = StreamUtil.ReadInt16(stream);

            //        locations.Add(TempLocation);
            //    }
            //    StreamUtil.AlignBy16(stream);
            //    chunksInfo = new List<ChunkInfo>();
            //    for (int i = 0; i < numChunks; i++)
            //    {
            //        var TempUnknown1 = new ChunkInfo();
            //        TempUnknown1.BboxLow = StreamUtil.ReadVector4(stream);
            //        TempUnknown1.BboxHigh = StreamUtil.ReadVector4(stream);
            //        TempUnknown1.UnknownFloat9 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat10 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat11 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat12 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat13 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat14 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat15 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat16 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat17 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat18 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat19 = StreamUtil.ReadFloat(stream);
            //        TempUnknown1.UnknownFloat20 = StreamUtil.ReadFloat(stream);

            //        TempUnknown1.UnknownInt1 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown1.UnknownInt2 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown1.UnknownInt3 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown1.UnknownInt4 = StreamUtil.ReadUInt32(stream);

            //        chunksInfo.Add(TempUnknown1);
            //    }

            //    unknown2s = new List<Unknown2>();

            //    for (int i = 0; i < numUnknown2; i++)
            //    {
            //        var TempUnknown2 = new Unknown2();
            //        TempUnknown2.UnknownInt1 = StreamUtil.ReadUInt32(stream); //Items in SubChunk
            //        TempUnknown2.UnknownInt2 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt3 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt4 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt5 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt6 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt7 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt8 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt9 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt10 = StreamUtil.ReadUInt32(stream);



            //        TempUnknown2.UnknownInt11 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt12 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt13 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt14 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt15 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt16 = StreamUtil.ReadUInt32(stream);
            //        TempUnknown2.UnknownInt17 = StreamUtil.ReadUInt32(stream);
            //        unknown2s.Add(TempUnknown2);
            //    }
            //}
        }

        //88 Bytes
        public struct Location
        {
            public string Name; //16
                                //Int32s
            public int numStreamingChunks;
            public int numChunks;
            public int posEndStreamingChunk;
            public int posChunks; 
            //Int16s
            public int numMaterials;
            public int numPatches;
            public int numWorldMDR;
            public int numInstance;
            public int numParticleModel;
            public int numParticleInstance;
            public int numLights;
            public int numHalo;
            public int numSplines;
            public int numShape; 
            public int numShapelightmap;
            public int numVisCurtains;
            public int numCollision;
            public int numSoundTrigger;
            public int numAIP; //AIP
            public int numWorldPainter;
            public int numScripts;
            public int numCameraTrigger;
            public int numNISTable;
            public int numMissions;
            public int numAudioBanks;
            public int numRadar;
            public int numAvalancheAnimation;
            public int U1;
            public int U2;
            public int U3;
            public int U4;
            public int U5;
        }

        //96 bytes
        public struct ChunkInfo
        {
            public Vector4 BboxLow;
            public Vector4 BboxHigh;

            public float UnknownFloat9;
            public float UnknownFloat10;
            public float UnknownFloat11;
            public float UnknownFloat12;

            public float UnknownFloat13;
            public float UnknownFloat14;
            public float UnknownFloat15;
            public float UnknownFloat16;

            public float UnknownFloat17;
            public float UnknownFloat18;
            public float UnknownFloat19;
            public float UnknownFloat20;

            public int UnknownInt1;
            public int UnknownInt2;
            public int UnknownInt3;
            public int UnknownInt4;
        }

        //68 Bytes
        public struct StreamingChunkInfo
        {
            //Int32
            public int numResources;
            public int subChunkID;
            public int chunkOffset;
            public int unpackedSize;
            public int numMaterials;
            public int numPatches;
            public int numWorldMDR;
            public int numInstance;
            public int numParticleModel;
            public int numParticleInstance;
            public int numLights;
            public int numHalo;
            public int numSplines;
            public int numShapes;
            public int numShapeLightmap;
            public int numVisCurtains;
            public int numCollision;

            //Doesnt Seem to Change but probably some variation on the below if used
            public int U1;
            public int U2;
            public int U3;
            public int U4;
            public int U5;
            public int U6;
            public int U7;
            public int U8;

            //public int numSoundTrigger;
            //public int numAIP; //AIP
            //public int numWorldPainter;
            //public int numScripts;
            //public int numCameraTrigger;
            //public int numNISTable;
            //public int numMissions;
            //public int numAudioBanks;
            //public int numRadar;
            //public int numAvalancheAnimation;
        }
    }
}
