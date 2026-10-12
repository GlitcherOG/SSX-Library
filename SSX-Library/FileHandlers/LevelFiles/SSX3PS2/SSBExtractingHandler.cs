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
    public class SSBExtractingHandler
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
                MaterialJsonHandler bin0JsonHandler = new MaterialJsonHandler();
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

                                Console.WriteLine(LevelExtractPath + "//Models//" + RID + ".obj");
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

                                    worldOldSSH.Load(EntryStream);
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

                                Console.WriteLine(extractPath + "//Collision//" + RID + ".obj");
                                worldBin12.CollisionObjectSave(LevelExtractPath + "//Collision//", phmHandler.GetName(TrackID, RID, 4, psmHandler));
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

                    Console.WriteLine(LevelExtractPath + "//ParticleInstances.json");
                    particleInstanceJsonHandler.CreateJson(LevelExtractPath + "//ParticleInstances.json");

                    Console.WriteLine(LevelExtractPath + "//Bin6.json");
                    bin6JsonHandler.CreateJson(LevelExtractPath + "//Bin6.json");

                    Console.WriteLine(LevelExtractPath + "//Bin11.json");
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
    }
}
