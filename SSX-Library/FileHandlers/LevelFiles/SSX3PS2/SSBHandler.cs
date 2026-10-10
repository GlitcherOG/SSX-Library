using SixLabors.ImageSharp.Drawing;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData;
using SSXLibrary.JsonFiles.SSX3;
using System.IO;
using System.IO.Pipelines;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2
{
    public class SSBHandler
    {
        /*
            
        All IDS
        0 - Materials
        1 - Patches
        2 - WorldMDR
        3 - Instance
        4 - Particle Model
        5 - Particles Instance
        6 - Lights
        7 - Halo
        8 - Splines
        9 - Shape
        10 - Old Shape Lightmaps
        11 - Vis Curtains
        12 - Collision?
        13 - Sound Triggers?
        14 - AIP
        15 - World Painter?
        16 - Scripts?
        17 - CameraTriggers?
        18 - NIS Table
        19 - Missions?
        20 - AudioBank
        21 - Radar?
        22 - Avalanche Animation

        Rebuild Order

        Shape
        Shape Lightmaps
        Materials
        Lights
        Halo
        Models
        Instance
        Particle Model
        Particle Instance
        Patches
        Splines
        Collision
        Vis Curtains
        Sound Triggers
        World Painter
        Camera Trigger
        Audio Bank
        AIP
        Scripts
        NIS Table
        Missions
        Radar
        Avalanche Animation
         */

        public void LoadAndExtractSSBFromSBD(string path, string extractPath)
        {
            SDBHandler sdbHandler = new SDBHandler();
            sdbHandler.LoadSBD(path.Replace(".ssb", ".sdb"));

            PHMHandler phmHandler = new PHMHandler();
            phmHandler.LoadPHM(path.Replace(".ssb", ".phm"));

            PSMHandler psmHandler = new PSMHandler();
            psmHandler.LoadPSM(path.Replace(".ssb", ".psm"));

            using (Stream stream = File.Open(path, FileMode.Open))
            {
                PatchesJsonHandler patchesJsonHandler = new PatchesJsonHandler();
                Bin0JsonHandler bin0JsonHandler = new Bin0JsonHandler();
                InstanceJsonHandler bin3JsonHandler = new InstanceJsonHandler();
                ParticleInstanceJsonHandler particleInstanceJsonHandler = new ParticleInstanceJsonHandler();
                Bin6JsonHandler bin6JsonHandler = new Bin6JsonHandler();
                SplineJsonHandler splineJsonHandler = new SplineJsonHandler();
                VisCurtainJsonHandler visCurtainJsonHandler = new VisCurtainJsonHandler();
                MDRJsonHandler mdrJsonHandler = new MDRJsonHandler();

                MemoryStream memoryStream = new MemoryStream();
                List<int> ints = new List<int>();
                int splitCount = 1;
                int FilePos = 0;
                Directory.CreateDirectory(extractPath + "//Textures");
                Directory.CreateDirectory(extractPath + "//Lightmaps");
                Directory.CreateDirectory(extractPath + "//Levels");
                for (int i = 0; i < sdbHandler.locations.Count; i++)
                {
                    Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[i].Name);
                    Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Models");
                    Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Sounds");
                    Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Collision");

                    int StreamingChunkNum = sdbHandler.locations[i].numStreamingChunks;
                    int StreamingChunkPos = sdbHandler.locations[i].posEndStreamingChunk - StreamingChunkNum + 1;

                    string LevelExtractPath = extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//";

                    for (int j = 0; j < StreamingChunkNum; j++)
                    {
                        var StreamingChunk = sdbHandler.streamingChunkInfos[StreamingChunkPos+j];
                        stream.Position = StreamingChunk.chunkOffset * 256;

                        while (true)
                        {
                            string MagicWords = StreamUtil.ReadString(stream, 4);

                            int Size = StreamUtil.ReadUInt32(stream);
                            byte[] Data = new byte[Size - 8];
                            byte[] DecompressedData = new byte[1];
                            Data = StreamUtil.ReadBytes(stream, Size - 8);

                            DecompressedData = Refpack.Decompress(Data);
                            StreamUtil.WriteBytes(memoryStream, DecompressedData);
                            if (MagicWords.ToUpper() == "CEND")
                            {
                                break;
                            }
                        }

                        //CheckSize
                        if(memoryStream.Length!= StreamingChunk.unpackedSize)
                        {
                            //throw new Exception("Size Missmatch");
                        }

                        memoryStream.Position = 0;
                        for (int k = 0; k < StreamingChunk.numResources; k++)
                        {
                            MemoryStream memoryStream1 = new MemoryStream();
                            int ID = StreamUtil.ReadUInt8(memoryStream);
                            int ChunkSize = StreamUtil.ReadInt24(memoryStream);
                            int TrackID = StreamUtil.ReadUInt8(memoryStream);
                            int RID = StreamUtil.ReadInt24(memoryStream);

                            byte[] NewData = StreamUtil.ReadBytes(memoryStream, ChunkSize);
                            StreamUtil.WriteBytes(memoryStream1, NewData);
                            memoryStream1.Position = 0;
                            string Path = RID + "-" + TrackID + "-" + FilePos;

                            if (ID == 0)
                            {
                                WorldBin0 worldbin0 = new WorldBin0();
                                worldbin0.LoadData(memoryStream1, TrackID, RID);

                                bin0JsonHandler.bin0Files.Add(worldbin0.ToJSON());
                            }
                            else if (ID == 1)
                            {
                                WorldPatch worldPatch = new WorldPatch();
                                worldPatch.LoadPatch(memoryStream1);

                                worldPatch.Name = phmHandler.GetName(TrackID, RID, 0, psmHandler);

                                patchesJsonHandler.Patches.Add(worldPatch.ToJSON());
                            }
                            else if (ID == 2)
                            {
                                WorldMDR worldMDR = new WorldMDR();

                                worldMDR.LoadData(memoryStream1);

                                worldMDR.Name = phmHandler.GetName(TrackID, RID, 2, psmHandler);

                                Console.WriteLine(LevelExtractPath + "//Models//" + RID + ".obj");
                                mdrJsonHandler.mainModelHeaders.Add(worldMDR.SaveModel(LevelExtractPath + "//Models//"));
                                //worldMDR.SaveModelGLB(ExtractPath + "//Models//" + RID + ".glb");
                            }
                            else if (ID == 3)
                            {
                                WorldInstance worldBin3 = new WorldInstance();
                                worldBin3.LoadData(memoryStream1);

                                worldBin3.Name = phmHandler.GetName(TrackID, RID, 1, psmHandler);

                                bin3JsonHandler.Instances.Add(worldBin3.ToJSON());
                            }
                            else if (ID == 5)
                            {
                                WorldParticleInstance worldParticleInstance = new WorldParticleInstance();
                                worldParticleInstance.LoadData(memoryStream1);

                                particleInstanceJsonHandler.ParticleInstances.Add(worldParticleInstance.ToJSON());
                            }
                            else if (ID == 6)
                            {
                                WorldBin6 worldBin6 = new WorldBin6();
                                worldBin6.LoadData(memoryStream1);

                                bin6JsonHandler.bin6Files.Add(worldBin6.ToJSON());
                            }
                            else if (ID == 8)
                            {
                                WorldSpline worldSpline = new WorldSpline();
                                worldSpline.LoadData(memoryStream1);

                                worldSpline.Name = phmHandler.GetName(TrackID, RID, 3, psmHandler);

                                splineJsonHandler.Splines.Add(worldSpline.ToJSON());
                            }
                            else if (ID == 9)
                            {
                                if (!File.Exists(extractPath + "//Textures//" + RID + ".png"))
                                {
                                    Console.WriteLine(extractPath + "//Textures//" + Path + ".png");
                                    WorldSSH worldOldSSH = new WorldSSH();

                                    worldOldSSH.Load(memoryStream1);

                                    worldOldSSH.SaveImage(extractPath + "//Textures//" + RID + ".png");
                                }

                            }
                            else if (ID == 10)
                            {
                                if (!File.Exists(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png"))
                                {
                                    Console.WriteLine(extractPath + "//Lightmaps//" + Path + ".png");
                                    WorldSSH worldOldSSH = new WorldSSH();

                                    worldOldSSH.Load(memoryStream1);
                                    //worldOldSSH.SaveImage(ExtractPath + "//Lightmaps//" + Path + ".png");
                                    worldOldSSH.SaveImage(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png");
                                }
                            }
                            else if (ID == 11)
                            {
                                WorldVisCurtain worldBin11 = new WorldVisCurtain();
                                worldBin11.LoadData(memoryStream1);

                                visCurtainJsonHandler.VisCurtains.Add(worldBin11.ToJSON());
                            }
                            else if (ID == 12)
                            {
                                WorldCollision worldBin12 = new WorldCollision();
                                worldBin12.LoadData(memoryStream1, TrackID, RID);

                                Console.WriteLine(extractPath + "//Collision//" + RID + ".obj");
                                worldBin12.CollisionObjectSave(LevelExtractPath + "//Collision//", phmHandler.GetName(TrackID, RID, 4, psmHandler));
                            }
                            else if (ID == 14)
                            {
                                WorldAIP worldAIP = new WorldAIP();
                                worldAIP.LoadData(NewData);

                                string FileName = RID + "AIP";

                                if (RID == 0)
                                {
                                    FileName = "AIP";
                                }
                                else if (RID == 1)
                                {
                                    FileName = "PeakRaceAIP";
                                }
                                else if (RID == 2)
                                {
                                    FileName = "PeakShowOffAIP";
                                }

                                Console.WriteLine(LevelExtractPath + FileName + ".json");
                                worldAIP.ToJson(LevelExtractPath + FileName + ".json");
                            }
                            else if (ID == 18)
                            {
                                Console.WriteLine(LevelExtractPath + "Bin18.json");
                                WorldBin18 worldBin18 = new WorldBin18();
                                worldBin18.LoadData(memoryStream1);

                                worldBin18.ToJson(LevelExtractPath + "Bin18.json");
                            }
                            else if (ID == 20)
                            {
                                Console.WriteLine(LevelExtractPath + "//Sounds//" + Path + ".bnk");
                                var file = File.Create(LevelExtractPath + "//Sounds//" + Path + ".bnk");
                                memoryStream1.CopyTo(file);
                                memoryStream1.Dispose();
                                memoryStream1 = new MemoryStream();
                                file.Close();
                            }
                            else
                            {
                                Console.WriteLine(LevelExtractPath + Path + ".bin" + ID);
                                var file = File.Create(LevelExtractPath + Path + ".bin" + ID);
                                memoryStream1.CopyTo(file);
                                memoryStream1.Dispose();
                                memoryStream1 = new MemoryStream();
                                file.Close();
                            }

                            FilePos++;
                        }
                        memoryStream = new MemoryStream();
                    }

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Patches.json");
                    patchesJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Patches.json");

                    Console.WriteLine(extractPath + "//Levels///" + sdbHandler.locations[i].Name + "//Bin0.json");
                    bin0JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Bin0.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Instances.json");
                    bin3JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Instances.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//ParticleInstances.json");
                    particleInstanceJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//ParticleInstances.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Bin6.json");
                    bin6JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Bin6.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Bin11.json");
                    visCurtainJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//VisCurtain.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Splines.json");
                    splineJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Splines.json");

                    Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Prefabs.json");
                    mdrJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//Prefabs.json");

                    patchesJsonHandler = new PatchesJsonHandler();
                    bin0JsonHandler = new Bin0JsonHandler();
                    bin3JsonHandler = new InstanceJsonHandler();
                    particleInstanceJsonHandler = new ParticleInstanceJsonHandler();
                    bin6JsonHandler = new Bin6JsonHandler();
                    visCurtainJsonHandler = new VisCurtainJsonHandler();
                    splineJsonHandler = new SplineJsonHandler();
                    mdrJsonHandler = new MDRJsonHandler();
                }
            }

            //Save Info Config File
            LevelJsonHandler levelJsonHandler = new LevelJsonHandler();
            levelJsonHandler.LevelInfoList = new List<LevelJsonHandler.LevelInfo>();
            for (int i = 0; i < sdbHandler.locations.Count; i++)
            {
                var NewLevelInfo = new LevelJsonHandler.LevelInfo();

                NewLevelInfo.TrackID = i;
                NewLevelInfo.LevelName = sdbHandler.locations[i].Name;

                levelJsonHandler.LevelInfoList.Add(NewLevelInfo);
            }
            levelJsonHandler.CreateJson(extractPath + "//Levels.json");

            SSX3Config ssx3Config = new SSX3Config();
            ssx3Config.CreateJson(extractPath + "//ConfigSSX3.ssx");
        }

        //Test Generated with Claude to delete and alter later
        struct IDSSB
        {
            public int ChunkID;
            public int ID;
            public string Files;
            public int TrackID;
            public int RID;
            public int Type;
        }

        const int BlockSize = 32768;
        const int HeaderSize = 8;                             // magic + int32
        const int MaxCompressed = BlockSize - HeaderSize - 16; // compressed data must fit in one aligned block
        const int MaxUncompressed = 0x14000;                  // largest decompressed block in the original files
        const int SDBOffsetUnit = 256;                        // SDB chunkOffset is in 256 byte units

        /// <summary>
        /// Packs a folder of raw resources into an SSB and writes a matching SDB next to it.
        /// Files are named ChunkID-Order-TrackID-RID.Type and hold the resource data without its 8 byte header.
        /// The SDB at SourceSDBPath (defaults to the .sdb next to BuildPath) is used as the template; its
        /// chunk offsets, unpacked sizes and resource counts are recalculated from the packed data.
        /// With KeepOriginalOffsets, each chunk stays at the template's offset when it still fits there
        /// (the gap left by a smaller chunk is zero filled); only chunks pushed back by a larger one move.
        /// </summary>
        public void PackSSB(string Folder, string BuildPath, string SourceSDBPath = null, bool KeepOriginalOffsets = false)
        {
            string BuildSDBPath = System.IO.Path.ChangeExtension(BuildPath, ".sdb");

            SDBHandler sdbHandler = new SDBHandler();
            sdbHandler.LoadSBD(SourceSDBPath ?? BuildSDBPath);

            List<IDSSB> iDSSBs = new List<IDSSB>();
            foreach (string path in Directory.GetFiles(Folder, "*.*"))
            {
                string[] parts = System.IO.Path.GetFileName(path).Split("-");
                iDSSBs.Add(new IDSSB
                {
                    ChunkID = int.Parse(parts[0]),
                    ID = int.Parse(parts[1]),
                    TrackID = int.Parse(parts[2]),
                    RID = int.Parse(parts[3].Split(".")[0]),
                    Type = int.Parse(parts[3].Split(".")[1]),
                    Files = path
                });
            }

            // Resource order within a chunk is the ID
            iDSSBs.Sort((a, b) => a.ChunkID != b.ChunkID ? a.ChunkID.CompareTo(b.ChunkID) : a.ID.CompareTo(b.ID));

            const int ResourceTypeCount = 23;      // resource type IDs 0-22
            const int StreamingResourceTypes = 13; // types 0-12 are counted per streaming chunk and in unpackedSize

            int chunkCount = sdbHandler.streamingChunkInfos.Count;
            int[,] chunkTypeCounts = new int[chunkCount, ResourceTypeCount];

            using (FileStream ssbStream = File.Create(BuildPath))
            {
                int start = 0;
                for (int chunkID = 0; chunkID < chunkCount; chunkID++)
                {
                    int end = start;
                    while (end < iDSSBs.Count && iDSSBs[end].ChunkID == chunkID)
                        end++;

                    if (end == start)
                    {
                        throw new Exception($"No files found for streaming chunk {chunkID}. Adding or removing chunks is not supported.");
                    }

                    // Rebuild each resource's 8 byte header in front of its data
                    int unpackedSize = 0;
                    using MemoryStream chunkData = new MemoryStream();
                    for (int i = start; i < end; i++)
                    {
                        byte[] fileBytes = File.ReadAllBytes(iDSSBs[i].Files);
                        chunkData.WriteByte((byte)iDSSBs[i].Type);
                        chunkData.WriteUInt24((uint)fileBytes.Length, SSX_Library.ByteOrder.LittleEndian);
                        chunkData.WriteByte((byte)iDSSBs[i].TrackID);
                        chunkData.WriteUInt24((uint)iDSSBs[i].RID, SSX_Library.ByteOrder.LittleEndian);
                        chunkData.Write(fileBytes, 0, fileBytes.Length);

                        chunkTypeCounts[chunkID, iDSSBs[i].Type]++;
                        if (iDSSBs[i].Type < StreamingResourceTypes)
                        {
                            unpackedSize += fileBytes.Length + 8;
                        }
                    }

                    var streamingChunk = sdbHandler.streamingChunkInfos[chunkID];

                    long originalPosition = (long)streamingChunk.chunkOffset * SDBOffsetUnit;
                    if (KeepOriginalOffsets && originalPosition > ssbStream.Position)
                    {
                        StreamUtil.WriteBytes(ssbStream, new byte[originalPosition - ssbStream.Position]);
                    }

                    streamingChunk.numResources = end - start;
                    streamingChunk.chunkOffset = (int)(ssbStream.Position / SDBOffsetUnit);
                    streamingChunk.unpackedSize = unpackedSize;
                    streamingChunk.numMaterials = chunkTypeCounts[chunkID, 0];
                    streamingChunk.numPatches = chunkTypeCounts[chunkID, 1];
                    streamingChunk.numWorldMDR = chunkTypeCounts[chunkID, 2];
                    streamingChunk.numInstance = chunkTypeCounts[chunkID, 3];
                    streamingChunk.numParticleModel = chunkTypeCounts[chunkID, 4];
                    streamingChunk.numParticleInstance = chunkTypeCounts[chunkID, 5];
                    streamingChunk.numLights = chunkTypeCounts[chunkID, 6];
                    streamingChunk.numHalo = chunkTypeCounts[chunkID, 7];
                    streamingChunk.numSplines = chunkTypeCounts[chunkID, 8];
                    streamingChunk.numShapes = chunkTypeCounts[chunkID, 9];
                    streamingChunk.numShapeLightmap = chunkTypeCounts[chunkID, 10];
                    streamingChunk.numVisCurtains = chunkTypeCounts[chunkID, 11];
                    streamingChunk.numCollision = chunkTypeCounts[chunkID, 12];
                    sdbHandler.streamingChunkInfos[chunkID] = streamingChunk;

                    WriteChunk(ssbStream, chunkData.ToArray());
                    start = end;
                }

                if (start != iDSSBs.Count)
                {
                    throw new Exception($"Found files for chunk {iDSSBs[start].ChunkID}, but the SDB only has {chunkCount} streaming chunks.");
                }
            }

            // Location counts are totals over the location's streaming chunks, except shapes and
            // shape lightmaps which are always 0 in the original files
            for (int i = 0; i < sdbHandler.locations.Count; i++)
            {
                var location = sdbHandler.locations[i];
                int first = location.posEndStreamingChunk - location.numStreamingChunks + 1;

                int[] totals = new int[ResourceTypeCount];
                for (int chunkID = first; chunkID <= location.posEndStreamingChunk; chunkID++)
                    for (int type = 0; type < ResourceTypeCount; type++)
                        totals[type] += chunkTypeCounts[chunkID, type];

                location.numMaterials = totals[0];
                location.numPatches = totals[1];
                location.numWorldMDR = totals[2];
                location.numInstance = totals[3];
                location.numParticleModel = totals[4];
                location.numParticleInstance = totals[5];
                location.numLights = totals[6];
                location.numHalo = totals[7];
                location.numSplines = totals[8];
                // numShape (9) and numShapelightmap (10) are left as they are
                location.numVisCurtains = totals[11];
                location.numCollision = totals[12];
                location.numSoundTrigger = totals[13];
                location.numAIP = totals[14];
                location.numWorldPainter = totals[15];
                location.numScripts = totals[16];
                location.numCameraTrigger = totals[17];
                location.numNISTable = totals[18];
                location.numMissions = totals[19];
                location.numAudioBanks = totals[20];
                location.numRadar = totals[21];
                location.numAvalancheAnimation = totals[22];
                sdbHandler.locations[i] = location;
            }

            sdbHandler.Save(BuildSDBPath);
        }

        void WriteChunk(Stream output, byte[] data)
        {
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream block = new MemoryStream();

            // do-while so an empty chunk still gets its CEND block
            RefpackStreamResult result;
            do
            {
                block.SetLength(0);
                result = RefpackStream.Compress(input, block, MaxCompressed, MaxUncompressed);

                if (result == RefpackStreamResult.Failed)
                {
                    throw new Exception($"Could not fit data at offset {input.Position} into a block");
                }

                StreamUtil.WriteString(output, result == RefpackStreamResult.Complete ? "CEND" : "CBXS");
                StreamUtil.WriteInt32(output, BlockSize);
                StreamUtil.WriteBytes(output, block.ToArray());

                // Write the padding instead of seeking past it, so the last block is full size too
                StreamUtil.WriteBytes(output, new byte[(BlockSize - (int)(output.Position % BlockSize)) % BlockSize]);
            }
            while (result != RefpackStreamResult.Complete);
        }
    }
}
