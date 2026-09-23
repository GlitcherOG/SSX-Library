using SSX_Library.Internal;
using SSX_Library.Internal.Utilities;
using SSXLibrary.FileHandlers.LevelFiles.SSXOnTourPS2.SSBOnTourData;
using System.Diagnostics;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SSXLibrary.FileHandlers.LevelFiles.OnTourPS2
{
    public class SSBHandler
    {
        /*
            
        All IDS

        9 - Shape
        10 - Old Shape Lightmaps

         */
        
        public void LoadAndExtractSSBFromSBD(string path, string extractPath)
        {
            SDBHandler sdbHandler = new SDBHandler();
            //sdbHandler.LoadSBD(path.Replace(".ssb", ".sdb"));

            PHMHandler phmHandler = new PHMHandler();
            //phmHandler.LoadPHM(path.Replace(".ssb", ".phm"));

            PSMHandler psmHandler = new PSMHandler();
            psmHandler.LoadPSM(path.Replace(".ssb", ".psm"));

            using (Stream stream = File.Open(path, FileMode.Open))
            {
                MemoryStream DataMemoryStream = new MemoryStream();
                List<int> ints = new List<int>();
                int a = 0;
                int splitCount = 1;
                int FilePos = 0;
                bool Start = false;
                int U0 = 0;
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
                    int ReadSize = 8;
                    if (!Start)
                    {
                        Start = true;
                        U0 = StreamUtil.ReadUInt32(stream);
                        if(U0!=3)
                        {
                            Debug.WriteLine("Not 3");
                        }
                        ReadSize = 12;
                    }

                    byte[] Buffer = StreamUtil.ReadBytes(stream, Size - ReadSize);

                    if (Refpack.HasRefpackSignature(Buffer))
                    {
                        Buffer = Refpack.Decompress(Buffer);
                    }

                    StreamUtil.WriteBytes(DataMemoryStream, Buffer);

                    if(MagicWords=="CEND")
                    {
                        Start= false;
                        DataMemoryStream.Position = 0;

                        while (DataMemoryStream.Position < DataMemoryStream.Length - 1)
                        {
                            int ID = StreamUtil.ReadUInt8(DataMemoryStream);

                            int encoded = StreamUtil.ReadUInt24(DataMemoryStream);

                            int Flags = encoded & 0x3;
                            int ChunkSize = encoded >> 2;
                            int TrackID = StreamUtil.ReadUInt16(DataMemoryStream);
                            int RID = StreamUtil.ReadUInt16(DataMemoryStream);

                            MemoryStream ChunkStream = new MemoryStream();

                            byte[] Bytes = StreamUtil.ReadBytes(DataMemoryStream, ChunkSize);

                            StreamUtil.WriteBytes(ChunkStream, Bytes);

                            if(ID==9)
                            {
                                if (!File.Exists(extractPath + "//Textures//" + RID + ".png"))
                                {
                                    Console.WriteLine(extractPath + "//Textures//" + RID + ".png");
                                    WorldSSH worldOldSSH = new WorldSSH();

                                    try
                                    {
                                        worldOldSSH.Load(ChunkStream);

                                        worldOldSSH.SaveImage(extractPath + "//Textures//" + TrackID + "-" + RID + ".png");
                                    }
                                    catch
                                    {

                                    }
                                }
                            }
                            else
                            {
                                var file = File.Create(extractPath + "\\" + TrackID + "-" + RID + ".bin" + ID);
                                ChunkStream.Position = 0;
                                ChunkStream.CopyTo(file);
                                file.Close();
                            }
                            ChunkStream.Dispose();
                            ChunkStream = new MemoryStream();
                        }

                        DataMemoryStream = new MemoryStream();


                        FilePos++;
                    }
                }
            }
        }
    }
}
