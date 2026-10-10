using System.IO;
using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData;
using SSXLibrary.JsonFiles.SSX3;

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
                int a = 0;
                int splitCount = 1;
                int FilePos = 0;
                int ChunkID = -1;
                Directory.CreateDirectory(extractPath + "//Textures");
                Directory.CreateDirectory(extractPath + "//Lightmaps");
                Directory.CreateDirectory(extractPath + "//Levels");
                while (true)
                {
                    if (stream.Position >= stream.Length - 1)
                    {
                        break;
                    }
                    string MagicWords = StreamUtil.ReadString(stream, 4);

                    int Size = StreamUtil.ReadUInt32(stream);
                    byte[] Data = new byte[Size - 8];
                    byte[] DecompressedData = new byte[1];
                    Data = StreamUtil.ReadBytes(stream, Size - 8);

                    DecompressedData = Refpack.Decompress(Data);
                    StreamUtil.WriteBytes(memoryStream, DecompressedData);
                    if (MagicWords.ToUpper() == "CEND")
                    {
                        ChunkID = sdbHandler.FindLocationChunk(a);
                        Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name);
                        Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Models");
                        Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Sounds");
                        Directory.CreateDirectory(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Collision");
                        memoryStream.Position = 0;

                        while (memoryStream.Position < memoryStream.Length)
                        {
                            MemoryStream memoryStream1 = new MemoryStream();
                            int ID = StreamUtil.ReadUInt8(memoryStream);
                            int ChunkSize = StreamUtil.ReadInt24(memoryStream);
                            int TrackID = StreamUtil.ReadUInt8(memoryStream);
                            int RID = StreamUtil.ReadInt24(memoryStream);

                            byte[] NewData = StreamUtil.ReadBytes(memoryStream, ChunkSize);
                            StreamUtil.WriteBytes(memoryStream1, NewData);
                            memoryStream1.Position = 0;

                            string LevelExtractPath = extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//";
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
                                Console.WriteLine(extractPath + "//Lightmaps//" + Path + ".png");
                                WorldSSH worldOldSSH = new WorldSSH();

                                worldOldSSH.Load(memoryStream1);
                                //worldOldSSH.SaveImage(ExtractPath + "//Lightmaps//" + Path + ".png");

                                if (!File.Exists(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png"))
                                {
                                    worldOldSSH.SaveImage(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png");
                                }
                            }
                            else if (ID == 11)
                            {
                                WorldVisCurtain worldBin11 = new WorldVisCurtain();
                                worldBin11.LoadData(memoryStream1);

                                visCurtainJsonHandler.VisCurtains.Add(worldBin11.ToJSON());
                            }
                            else if (ID==12)
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
                        int TempChunkID = sdbHandler.FindLocationChunk(a+1);
                        if (TempChunkID != ChunkID || stream.Position >= stream.Length - 1)
                        {
                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Patches.json");
                            patchesJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Patches.json");

                            Console.WriteLine(extractPath + "//Levels///" + sdbHandler.locations[ChunkID].Name + "//Bin0.json");
                            bin0JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Bin0.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Instances.json");
                            bin3JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Instances.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//ParticleInstances.json");
                            particleInstanceJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//ParticleInstances.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Bin6.json");
                            bin6JsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Bin6.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Bin11.json");
                            visCurtainJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//VisCurtain.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Splines.json");
                            splineJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Splines.json");

                            Console.WriteLine(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Prefabs.json");
                            mdrJsonHandler.CreateJson(extractPath + "//Levels//" + sdbHandler.locations[ChunkID].Name + "//Prefabs.json");

                            patchesJsonHandler = new PatchesJsonHandler();
                            bin0JsonHandler = new Bin0JsonHandler();
                            bin3JsonHandler = new InstanceJsonHandler();
                            particleInstanceJsonHandler = new ParticleInstanceJsonHandler();
                            bin6JsonHandler = new Bin6JsonHandler();
                            visCurtainJsonHandler = new VisCurtainJsonHandler();
                            splineJsonHandler = new SplineJsonHandler();
                            mdrJsonHandler = new MDRJsonHandler();
                        }
                        a++;
                        memoryStream = new MemoryStream();
                    }
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

        struct IDSSB
        {
            public int ChunkID;
            public int ID;
            public string Files;
            public int TrackID;
            public int RID;
            public int Type;
        }

        //public void PackSSB(string Folder, string BuildPath)
        //{
        //    MemoryStream memoryStream = new MemoryStream();
        //    string[] AllFiles = Directory.GetFiles(Folder, "*.*");

        //    List<IDSSB> iDSSBs = new List<IDSSB>();
        //    for (int i = 0; i < AllFiles.Length; i++)
        //    {
        //        IDSSB TempiDSSB = new IDSSB();

        //        string FileID = Path.GetFileName(AllFiles[i]);

        //        TempiDSSB.ChunkID = int.Parse(FileID.Split("-")[0]);
        //        TempiDSSB.ID = int.Parse(FileID.Split("-")[1]);
        //        TempiDSSB.Files = AllFiles[i];

        //        iDSSBs.Add(TempiDSSB);
        //    }

        //    iDSSBs.Sort((a, b) => a.ID.CompareTo(b.ID));

        //    int ChunkID = 0;
        //    int WritePoint = 0;
        //    bool WriteChunk = false;
        //    int ReadLenght = 40000;
        //    //Final Output Regardless needs to be 32768 bytes long when compressed
        //    byte[] output = new byte[ReadLenght];


        //    for (int i = 0; i < iDSSBs.Count; i++)
        //    {
        //        //Start reading files into byte stream
        //        //Once hitting lenght or passing it compress to correct chunk type
        //        //If file is end of chunk
        //        bool EndChunk = false;
        //        bool ChunkFull = false;
        //        using (Stream stream = File.Open(iDSSBs[i].Files, FileMode.Open))
        //        {
        //            if (WritePoint + stream.Length < ReadLenght)
        //            {
        //                //Write chunk
        //                byte[] Input = StreamUtil.ReadBytes(stream, (int)stream.Length);
        //                Array.Copy(Input, 0, output, 0, Input.Length);
        //                WritePoint += Input.Length;
        //            }
        //            else
        //            {
        //                ChunkFull = true;
        //                WriteChunk = true;
        //                i--;
        //                byte[] CompressedOutput = new byte[ReadLenght];
        //                Array.Copy(output, 0, CompressedOutput, 0, WritePoint);
        //                //Compress chunk and confirm safe
        //                output = Refpack.Compress(output);
        //                //If not error
        //                if(output.Length > 32768)
        //                {
        //                    throw new Exception("Lenght Error");
        //                }
        //                //will need to swap out for better data
        //            }
        //        }

        //        //Extra Conditions
        //        if (!ChunkFull)
        //        {
        //            if (iDSSBs.Count < i + 1)
        //            {
        //                WriteChunk = true;
        //                EndChunk = true;
        //            }
        //            else if (iDSSBs.Count < i)
        //            {
        //                if (ChunkID != iDSSBs[i + 1].ChunkID)
        //                {
        //                    WriteChunk = true;
        //                    EndChunk = true;
        //                }
        //            }
        //        }

        //        if (WriteChunk)
        //        {
        //            if (EndChunk)
        //            {
        //                StreamUtil.WriteString(memoryStream, "CBSX");
        //            }
        //            else
        //            {
        //                StreamUtil.WriteString(memoryStream, "CEND");
        //            }

        //            StreamUtil.WriteInt32(memoryStream, 32768);

        //            StreamUtil.WriteBytes(memoryStream, output);

        //            StreamUtil.AlignBy(memoryStream, 32768);

        //            output = new byte[ReadLenght];
        //            ChunkFull = false;
        //            EndChunk = false;
        //            WriteChunk = false;
        //            WritePoint = 0;
        //        }
        //    }
        //    if (File.Exists(BuildPath))
        //    {
        //        File.Delete(BuildPath);
        //    }
        //    var file = File.Create(BuildPath);
        //    memoryStream.Position = 0;
        //    memoryStream.CopyTo(file);
        //    memoryStream.Dispose();
        //    file.Close();
        //    GC.Collect();
        //}

        const int BlockSize = 32768;
        const int HeaderSize = 8;                          // magic + int32
        const int MaxCompressed = BlockSize - HeaderSize-16;  // compressed data must fit in one aligned block

        public void PackSSB(string Folder, string BuildPath)
        {
            List<IDSSB> iDSSBs = new List<IDSSB>();
            foreach (string path in Directory.GetFiles(Folder, "*.*"))
            {
                string[] parts = Path.GetFileName(path).Split("-");
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
            iDSSBs.Sort((a, b) => a.ID.CompareTo(b.ID));

            using MemoryStream memoryStream = new MemoryStream();

            // Group consecutive files by ChunkID, join each group, then split it into blocks
            int start = 0;
            while (start < iDSSBs.Count)
            {
                int end = start;
                while (end < iDSSBs.Count && iDSSBs[end].ChunkID == iDSSBs[start].ChunkID)
                    end++;

                using MemoryStream chunkData = new MemoryStream();
                for (int i = start; i < end; i++)
                {
                    byte[] fileBytes = File.ReadAllBytes(iDSSBs[i].Files);
                    chunkData.WriteByte((byte)iDSSBs[i].Type);
                    chunkData.WriteUInt24((uint)fileBytes.Length, SSX_Library.ByteOrder.LittleEndian);
                    chunkData.WriteByte((byte)iDSSBs[i].TrackID);
                    chunkData.WriteUInt24((uint)iDSSBs[i].RID, SSX_Library.ByteOrder.LittleEndian);
                    chunkData.Write(fileBytes, 0, fileBytes.Length);
                }

                WriteChunk(memoryStream, chunkData.ToArray());
                start = end;
            }

            File.WriteAllBytes(BuildPath, memoryStream.ToArray()); // overwrites, no need to delete first
        }

        void WriteChunk(Stream output, byte[] data)
        {
            using MemoryStream input = new MemoryStream(data);
            using MemoryStream block = new MemoryStream();

            while (input.Position < input.Length)
            {
                block.SetLength(0);
                RefpackStreamResult result = RefpackStream.Compress(input, block, MaxCompressed);

                if (result == RefpackStreamResult.Failed)
                {
                    throw new Exception($"Could not fit data at offset {input.Position} into a block");
                }

                bool lastBlock = result == RefpackStreamResult.Complete;

                StreamUtil.WriteString(output, lastBlock ? "CEND" : "CBXS");
                StreamUtil.WriteInt32(output, BlockSize);
                StreamUtil.WriteBytes(output, block.ToArray());
                StreamUtil.AlignBy(output, BlockSize);
            }
        }
    }
}
