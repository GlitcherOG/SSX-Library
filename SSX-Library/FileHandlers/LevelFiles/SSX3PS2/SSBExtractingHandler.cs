using SixLabors.ImageSharp.Drawing;
using SSXLibrary.Internal;
using SSXLibrary.Internal.Utilities;
using SSXLibrary.Internal.Utilities.StreamExtensions;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data;
using SSXLibrary.JsonFiles.SSX3;
using System.IO;
using System.IO.Pipelines;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2
{
    public class SSBExtractingHandler
    {
        /*
            
        All IDS
        0 - Materials - Extracting
        1 - Patches - Extracting
        2 - WorldMDR - Partically
        3 - Instance - Extracting (Issues with Vertex Lighting and no idea if collisions are linked)
        4 - Particle Model - Extracting
        5 - Particles Instance - Extracting (Needs a Review)
        6 - Lights - Extracting
        7 - Halo - Extracting
        8 - Splines - Extacting
        9 - Shape - Extracting
        10 - Old Shape Lightmaps - Extracting
        11 - Vis Curtains - Extracting
        12 - Collision? - Extracting Mostly but unknown how its linked
        13 - Sound Triggers? - Loading
        14 - AIP - Extracting
        15 - World Painter?
        16 - Scripts?
        17 - CameraTriggers?
        18 - NIS Table - Extracting
        19 - Missions?
        20 - AudioBank - Extracting
        21 - Radar?
        22 - Avalanche Animation
         */

        SDBHandler sdbHandler = new SDBHandler();
        PSMHandler psmHandler = new PSMHandler();
        PHMHandler phmHandler = new PHMHandler();

        public void LoadAndExtractSSBFromSBD(string path, string extractPath)
        {
            sdbHandler = new SDBHandler();
            sdbHandler.LoadSBD(path.Replace(".ssb", ".sdb"));

            phmHandler = new PHMHandler();
            phmHandler.LoadPHM(path.Replace(".ssb", ".phm"));
            
            psmHandler = new PSMHandler();
            psmHandler.LoadPSM(path.Replace(".ssb", ".psm"));

            using (Stream stream = File.Open(path, FileMode.Open))
            {
                PatchesJsonHandler patchesJsonHandler = new PatchesJsonHandler();
                MaterialJsonHandler bin0JsonHandler = new MaterialJsonHandler();
                InstanceJsonHandler bin3JsonHandler = new InstanceJsonHandler();
                Bin4JsonHandler bin4JsonHandler = new Bin4JsonHandler();
                ParticleInstanceJsonHandler particleInstanceJsonHandler = new ParticleInstanceJsonHandler();
                Bin6JsonHandler bin6JsonHandler = new Bin6JsonHandler();
                Bin7JsonHandler bin7JsonHandler = new Bin7JsonHandler();
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
                    string LevelExtractPath = extractPath + "//Levels//" + sdbHandler.locations[i].Name + "//";

                    Directory.CreateDirectory(LevelExtractPath);
                    Directory.CreateDirectory(LevelExtractPath + "//Models");
                    Directory.CreateDirectory(LevelExtractPath + "//Sounds");
                    Directory.CreateDirectory(LevelExtractPath + "//Collision");

                    int StreamingChunkNum = sdbHandler.locations[i].numStreamingChunks;
                    int StreamingChunkPos = sdbHandler.locations[i].posEndStreamingChunk - StreamingChunkNum + 1;

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

                        //Multithread

                        //CheckSize
                        if(memoryStream.Length!= StreamingChunk.unpackedSize)
                        {
                            //throw new Exception("Size Missmatch");
                        }

                        memoryStream.Position = 0;
                        for (int k = 0; k < StreamingChunk.numResources; k++)
                        {
                            MemoryStream EntryStream = new MemoryStream();

                            int ID = StreamUtil.ReadUInt8(memoryStream);
                            int EntrySize = StreamUtil.ReadInt24(memoryStream);
                            int TrackID = StreamUtil.ReadUInt8(memoryStream);
                            int RID = StreamUtil.ReadInt24(memoryStream);

                            StreamUtil.WriteBytes(EntryStream, StreamUtil.ReadBytes(memoryStream, EntrySize));

                            EntryStream.Position = 0;
                            string Path = RID + "-" + TrackID + "-" + FilePos;

                            if (ID == 0)
                            {
                                WorldMaterial worldbin0 = new WorldMaterial();
                                worldbin0.LoadData(EntryStream, TrackID, RID);

                                bin0JsonHandler.Materials.Add(worldbin0.ToJSON());
                            }
                            else if (ID == 1)
                            {
                                WorldPatch worldPatch = new WorldPatch();
                                worldPatch.LoadPatch(EntryStream);

                                worldPatch.Name = phmHandler.GetName(TrackID, RID, 0, psmHandler);

                                patchesJsonHandler.Patches.Add(worldPatch.ToJSON());
                            }
                            else if (ID == 2)
                            {
                                WorldMDR worldMDR = new WorldMDR();

                                worldMDR.LoadData(EntryStream);

                                worldMDR.Name = phmHandler.GetName(TrackID, RID, 2, psmHandler);

                                Console.WriteLine(LevelExtractPath + "//Models//" + worldMDR.Name + ".obj");
                                mdrJsonHandler.mainModelHeaders.Add(worldMDR.SaveModel(LevelExtractPath + "//Models//"));
                                //worldMDR.SaveModelGLB(ExtractPath + "//Models//" + RID + ".glb");
                            }
                            else if (ID == 3)
                            {
                                WorldInstance worldBin3 = new WorldInstance();
                                worldBin3.LoadData(EntryStream);

                                worldBin3.Name = phmHandler.GetName(TrackID, RID, 1, psmHandler);

                                bin3JsonHandler.Instances.Add(worldBin3.ToJSON());
                            }
                            else if (ID == 4)
                            {
                                WorldBin4 worldBin4 = new WorldBin4();
                                worldBin4.LoadData(EntryStream);

                                bin4JsonHandler.bin4s.Add(worldBin4.ToJSON());
                            }
                            else if (ID == 5)
                            {
                                WorldParticleInstance worldParticleInstance = new WorldParticleInstance();
                                worldParticleInstance.LoadData(EntryStream);

                                particleInstanceJsonHandler.ParticleInstances.Add(worldParticleInstance.ToJSON());
                            }
                            else if (ID == 6)
                            {
                                WorldBin6 worldBin6 = new WorldBin6();
                                worldBin6.LoadData(EntryStream);

                                bin6JsonHandler.bin6Files.Add(worldBin6.ToJSON());
                            }
                            else if (ID == 7)
                            {
                                WorldBin7 worldBin7 = new WorldBin7();
                                worldBin7.LoadData(EntryStream, TrackID, RID);

                                bin7JsonHandler.bin7Files.Add(worldBin7.ToJSON());
                            }
                            else if (ID == 8)
                            {
                                WorldSpline worldSpline = new WorldSpline();
                                worldSpline.LoadData(EntryStream);

                                worldSpline.Name = phmHandler.GetName(TrackID, RID, 3, psmHandler);

                                splineJsonHandler.Splines.Add(worldSpline.ToJSON());
                            }
                            else if (ID == 9)
                            {
                                if (!File.Exists(extractPath + "//Textures//" + RID + ".png"))
                                {
                                    Console.WriteLine(extractPath + "//Textures//" + Path + ".png");
                                    WorldSSH worldOldSSH = new WorldSSH();

                                    worldOldSSH.Load(EntryStream);

                                    worldOldSSH.ExportImage(extractPath + "//Textures//" + RID + ".png");
                                }

                            }
                            else if (ID == 10)
                            {
                                if (!File.Exists(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png"))
                                {
                                    Console.WriteLine(extractPath + "//Lightmaps//" + Path + ".png");
                                    WorldSSH worldOldSSH = new WorldSSH();

                                    worldOldSSH.Load(EntryStream, true);
                                    //worldOldSSH.SaveImage(ExtractPath + "//Lightmaps//" + Path + ".png");
                                    worldOldSSH.ExportImage(extractPath + "//Lightmaps//" + RID.ToString().PadLeft(4, '0') + ".png");
                                }
                            }
                            else if (ID == 11)
                            {
                                WorldVisCurtain worldBin11 = new WorldVisCurtain();
                                worldBin11.LoadData(EntryStream);

                                visCurtainJsonHandler.VisCurtains.Add(worldBin11.ToJSON());
                            }
                            else if (ID == 12)
                            {
                                WorldCollision worldBin12 = new WorldCollision();
                                worldBin12.LoadData(EntryStream, TrackID, RID);

                                Console.WriteLine(LevelExtractPath + "//Collision//" + phmHandler.GetName(TrackID, RID, 4, psmHandler) + ".obj");
                                worldBin12.CollisionObjectSave(LevelExtractPath + "//Collision//", phmHandler.GetName(TrackID, RID, 4, psmHandler));
                            }
                            else if (ID == 13)
                            {
                                WorldBin13 worldBin13 = new WorldBin13();
                                worldBin13.LoadData(EntryStream);

                                //Something
                            }
                            else if (ID == 14)
                            {
                                WorldAIP worldAIP = new WorldAIP();
                                worldAIP.LoadData(EntryStream);

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
                                worldBin18.LoadData(EntryStream);

                                worldBin18.ToJson(LevelExtractPath + "Bin18.json");
                            }
                            else if (ID == 20)
                            {
                                Console.WriteLine(LevelExtractPath + "//Sounds//" + Path + ".bnk");
                                var file = File.Create(LevelExtractPath + "//Sounds//" + Path + ".bnk");
                                EntryStream.CopyTo(file);
                                EntryStream.Dispose();
                                EntryStream = new MemoryStream();
                                file.Close();
                            }
                            else
                            {
                                Console.WriteLine(LevelExtractPath + Path + ".bin" + ID);
                                var file = File.Create(LevelExtractPath + Path + ".bin" + ID);
                                EntryStream.CopyTo(file);
                                EntryStream.Dispose();
                                EntryStream = new MemoryStream();
                                file.Close();
                            }

                            FilePos++;
                        }
                        memoryStream = new MemoryStream();
                    }

                    Console.WriteLine(LevelExtractPath + "//Patches.json");
                    patchesJsonHandler.CreateJson(LevelExtractPath + "//Patches.json");

                    Console.WriteLine(extractPath + "//Levels///" + sdbHandler.locations[i].Name + "//Materials.json");
                    bin0JsonHandler.CreateJson(LevelExtractPath + "//Materials.json");

                    Console.WriteLine(LevelExtractPath + "//Instances.json");
                    bin3JsonHandler.CreateJson(LevelExtractPath + "//Instances.json");

                    Console.WriteLine(LevelExtractPath + "//bin4.json");
                    bin4JsonHandler.CreateJson(LevelExtractPath + "//bin4.json");

                    Console.WriteLine(LevelExtractPath + "//ParticleInstances.json");
                    particleInstanceJsonHandler.CreateJson(LevelExtractPath + "//ParticleInstances.json");

                    Console.WriteLine(LevelExtractPath + "//Bin6.json");
                    bin6JsonHandler.CreateJson(LevelExtractPath + "//Bin6.json");

                    Console.WriteLine(LevelExtractPath + "//Bin7.json");
                    bin7JsonHandler.CreateJson(LevelExtractPath + "//Bin7.json");

                    Console.WriteLine(LevelExtractPath + "//VisCurtain.json");
                    visCurtainJsonHandler.CreateJson(LevelExtractPath + "//VisCurtain.json");

                    Console.WriteLine(LevelExtractPath + "//Splines.json");
                    splineJsonHandler.CreateJson(LevelExtractPath + "//Splines.json");

                    Console.WriteLine(LevelExtractPath + "//Prefabs.json");
                    mdrJsonHandler.CreateJson(LevelExtractPath + "//Prefabs.json");

                    patchesJsonHandler = new PatchesJsonHandler();
                    bin0JsonHandler = new MaterialJsonHandler();
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

        static async Task ExtractPrcoess(string LevelExtractPath)
        {

        }
    }
}
