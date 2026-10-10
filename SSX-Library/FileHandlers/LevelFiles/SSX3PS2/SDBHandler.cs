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
                    TempLocation.numChunks = StreamUtil.ReadUInt32(stream);
                    TempLocation.numStreamingChunks = StreamUtil.ReadUInt32(stream);
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
                    TempUnknown1.Child1EdgePlane = StreamUtil.ReadVector4(stream);
                    TempUnknown1.Child2EdgePlane = StreamUtil.ReadVector4(stream);
                    TempUnknown1.SplitPlane = StreamUtil.ReadVector4(stream);

                    TempUnknown1.Child1 = StreamUtil.ReadUInt32(stream);
                    TempUnknown1.Child2 = StreamUtil.ReadUInt32(stream);
                    TempUnknown1.StreamingChunkID = StreamUtil.ReadUInt32(stream);
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
                    TempUnknown2.numLights = StreamUtil.ReadUInt16(stream); //6
                    TempUnknown2.numHalo = StreamUtil.ReadUInt16(stream); //7
                    TempUnknown2.numSplines = StreamUtil.ReadUInt16(stream); //8
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
            using MemoryStream stream = new MemoryStream();

            StreamUtil.WriteBytes(stream, UnknownBytes);
            StreamUtil.WriteFloat32(stream, UnknownFloat);
            StreamUtil.WriteInt32(stream, locations.Count);
            StreamUtil.WriteInt32(stream, chunksInfo.Count);
            StreamUtil.WriteInt32(stream, streamingChunkInfos.Count);
            StreamUtil.WriteBytes(stream, UnknownBytes2);

            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                StreamUtil.WriteString(stream, location.Name, 16);
                StreamUtil.WriteInt32(stream, location.numChunks);
                StreamUtil.WriteInt32(stream, location.numStreamingChunks);
                StreamUtil.WriteInt32(stream, location.posEndStreamingChunk);
                StreamUtil.WriteInt32(stream, location.posChunks);

                StreamUtil.WriteInt16(stream, location.numMaterials);
                StreamUtil.WriteInt16(stream, location.numPatches);
                StreamUtil.WriteInt16(stream, location.numWorldMDR);
                StreamUtil.WriteInt16(stream, location.numInstance);
                StreamUtil.WriteInt16(stream, location.numParticleModel);
                StreamUtil.WriteInt16(stream, location.numParticleInstance);
                StreamUtil.WriteInt16(stream, location.numLights);
                StreamUtil.WriteInt16(stream, location.numHalo);
                StreamUtil.WriteInt16(stream, location.numSplines);
                StreamUtil.WriteInt16(stream, location.numShape);
                StreamUtil.WriteInt16(stream, location.numShapelightmap);
                StreamUtil.WriteInt16(stream, location.numVisCurtains);
                StreamUtil.WriteInt16(stream, location.numCollision);
                StreamUtil.WriteInt16(stream, location.numSoundTrigger);
                StreamUtil.WriteInt16(stream, location.numAIP);
                StreamUtil.WriteInt16(stream, location.numWorldPainter);
                StreamUtil.WriteInt16(stream, location.numScripts);
                StreamUtil.WriteInt16(stream, location.numCameraTrigger);
                StreamUtil.WriteInt16(stream, location.numNISTable);
                StreamUtil.WriteInt16(stream, location.numMissions);
                StreamUtil.WriteInt16(stream, location.numAudioBanks);
                StreamUtil.WriteInt16(stream, location.numRadar);
                StreamUtil.WriteInt16(stream, location.numAvalancheAnimation);

                StreamUtil.WriteInt16(stream, location.U1);
                StreamUtil.WriteInt16(stream, location.U2);
                StreamUtil.WriteInt16(stream, location.U3);
                StreamUtil.WriteInt16(stream, location.U4);
                StreamUtil.WriteInt16(stream, location.U5);
            }

            // Pad with zeros to 16 bytes
            StreamUtil.WriteBytes(stream, new byte[(16 - (int)(stream.Position % 16)) % 16]);

            for (int i = 0; i < chunksInfo.Count; i++)
            {
                var chunk = chunksInfo[i];
                StreamUtil.WriteVector4(stream, chunk.BboxLow);
                StreamUtil.WriteVector4(stream, chunk.BboxHigh);
                StreamUtil.WriteVector4(stream, chunk.Child1EdgePlane);
                StreamUtil.WriteVector4(stream, chunk.Child2EdgePlane);
                StreamUtil.WriteVector4(stream, chunk.SplitPlane);
                StreamUtil.WriteInt32(stream, chunk.Child1);
                StreamUtil.WriteInt32(stream, chunk.Child2);
                StreamUtil.WriteInt32(stream, chunk.StreamingChunkID);
                StreamUtil.WriteInt32(stream, chunk.UnknownInt4);
            }

            for (int i = 0; i < streamingChunkInfos.Count; i++)
            {
                var streamingChunk = streamingChunkInfos[i];
                StreamUtil.WriteInt16(stream, streamingChunk.numResources);
                StreamUtil.WriteInt24(stream, streamingChunk.subChunkID);
                StreamUtil.WriteInt24(stream, streamingChunk.chunkOffset);
                StreamUtil.WriteInt32(stream, streamingChunk.unpackedSize);

                StreamUtil.WriteInt16(stream, streamingChunk.numMaterials); //0
                StreamUtil.WriteInt16(stream, streamingChunk.numPatches); //1
                StreamUtil.WriteInt16(stream, streamingChunk.numWorldMDR); //2
                StreamUtil.WriteInt16(stream, streamingChunk.numInstance); //3
                StreamUtil.WriteInt16(stream, streamingChunk.numParticleModel); //4
                StreamUtil.WriteInt16(stream, streamingChunk.numParticleInstance); //5
                StreamUtil.WriteInt16(stream, streamingChunk.numLights); //6
                StreamUtil.WriteInt16(stream, streamingChunk.numHalo); //7
                StreamUtil.WriteInt16(stream, streamingChunk.numSplines); //8
                StreamUtil.WriteInt16(stream, streamingChunk.numShapes); //9
                StreamUtil.WriteInt16(stream, streamingChunk.numShapeLightmap); //10
                StreamUtil.WriteInt16(stream, streamingChunk.numVisCurtains); //11
                StreamUtil.WriteInt16(stream, streamingChunk.numCollision); //12

                StreamUtil.WriteInt16(stream, streamingChunk.U1);
                StreamUtil.WriteInt32(stream, streamingChunk.U2);
                StreamUtil.WriteInt32(stream, streamingChunk.U3);
                StreamUtil.WriteInt32(stream, streamingChunk.U4);
                StreamUtil.WriteInt32(stream, streamingChunk.U5);
                StreamUtil.WriteInt32(stream, streamingChunk.U6);
                StreamUtil.WriteInt32(stream, streamingChunk.U7);
                StreamUtil.WriteInt32(stream, streamingChunk.U8);
            }

            File.WriteAllBytes(path, stream.ToArray());
        }

        //88 Bytes
        public struct Location
        {
            public string Name; //16
                                //Int32s
            public int numChunks; //ChunkInfo entries
            public int numStreamingChunks;
            public int posEndStreamingChunk; //Index of this location's last streaming chunk
            public int posChunks; //Index of this location's first ChunkInfo
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
        //A node in the location's texture streaming tree. Nodes are numbered breadth first from Location.posChunks
        public struct ChunkInfo
        {
            public Vector4 BboxLow; //Node bounds (child bounds overlap around the split)
            public Vector4 BboxHigh;

            //Planes are normal xyz + d, a point p is on the plane when dot(normal, p) + d = 0
            //Only used on split nodes, all zeros on leaves
            public Vector4 Child1EdgePlane; //Child1's far edge (in front = outside Child1's box)
            public Vector4 Child2EdgePlane; //Child2's near edge (in front = outside Child2's box)
            public Vector4 SplitPlane; //dot(normal, p) + d < 0 goes to Child1, otherwise Child2

            public int Child1; //Index of the first child ChunkInfo, -1 on leaves
            public int Child2; //Index of the second child ChunkInfo, -1 on leaves
            public int StreamingChunkID; //Leaf's texture streaming chunk, -1 on split nodes (and on sky/TRANSP)
            public int UnknownInt4; //Always 0 in bam.sdb
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
