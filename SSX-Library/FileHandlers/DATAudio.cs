using SSX_Library.Internal.Utilities;
using System.Diagnostics;

namespace SSXLibrary.FileHandlers
{
    public class DATAudio
    {
        public void ExtractGuess(string OpenPath, string ExtractFolder, string SXDirectory, string TempDirectory)
        {
            string TempAudioDirectory = TempDirectory + "/TempAudio";

            Directory.CreateDirectory(TempAudioDirectory);
            File.Copy(SXDirectory + "/sx_2002.exe", TempAudioDirectory + "/sx.exe", true);
            List<long> Offsets = new List<long>();
            using (Stream stream = File.Open(OpenPath, FileMode.Open))
            {
                while (true)
                {
                    long Offset = ByteUtil.FindPosition(stream, new byte[4] { 0x53, 0x43, 0x48, 0x6C }, -1, -1);

                    if (Offset != -1)
                    {
                        Offsets.Add(Offset);

                    }
                    else
                    {
                        break;
                    }
                }

                for (int i = 0; i < Offsets.Count; i++)
                {
                    stream.Position = Offsets[i];

                    long ByteSize = 0;

                    if (i == Offsets.Count - 1)
                    {
                        ByteSize = stream.Length - Offsets[i];
                    }
                    else
                    {
                        ByteSize = Offsets[i + 1] - Offsets[i];
                    }

                    MemoryStream StreamMemory = new MemoryStream();

                    byte[] Data = StreamUtil.ReadBytes(stream, (int)ByteSize);

                    StreamUtil.WriteBytes(StreamMemory, Data);

                    if (File.Exists(TempAudioDirectory + "/Temp.mus"))
                    {
                        File.Delete(TempAudioDirectory + "/Temp.mus");
                    }

                    //Wait to ensure file is gone
                    while (File.Exists(TempAudioDirectory+"/Temp.mus"))
                    {

                    }

                    var file = File.Create(TempAudioDirectory+"/Temp.mus");
                    StreamMemory.Position = 0;
                    StreamMemory.CopyTo(file);
                    StreamMemory.Dispose();
                    file.Close();

                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = false;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    FileInfo f = new FileInfo(TempDirectory);
                    string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

                    cmd.StandardInput.WriteLine(drive);
                    cmd.StandardInput.WriteLine("cd " + TempAudioDirectory);
                    cmd.StandardInput.WriteLine("sx.exe -wave -s16l_int -playlocmaincpu  Temp.mus -=Temp.wav");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();

                    File.Copy(TempAudioDirectory + "/Temp.wav", ExtractFolder + "/" + $"{i:000}" + ".wav", true);
                }
            }

            Directory.Delete(TempAudioDirectory, true);
        }

        public HDRHandler GenerateDATAndHDR(string[] FileOpen, string FileSave, HDRHandler hdrHandler, string SXDirectory, string TempDirectory)
        {
            string TempAudioDirectory = TempDirectory + "/TempAudio";

            Directory.CreateDirectory(TempAudioDirectory);
            Directory.CreateDirectory(TempAudioDirectory + "/Holder");
            File.Copy(SXDirectory + "/sx_2002.exe", TempAudioDirectory+"/sx.exe", true);
            if (File.Exists(FileSave))
            {
                File.Delete(FileSave);
            }
            while (File.Exists(FileSave))
            {

            }
            var file = File.Create(FileSave);
            while (!File.Exists(FileSave))
            {

            }
            file.Close();

            List<string> HolderPaths = new List<string>();
            //Create File and memory stream
            using (Stream stream = File.Open(FileSave, FileMode.Open))
            {
                for (int i = 0; i < FileOpen.Length; i++)
                {
                    //Copy File
                    if (File.Exists(TempAudioDirectory + "/Temp.mus"))
                    {
                        File.Delete(TempAudioDirectory + "/Temp.mus");
                    }

                    while (File.Exists(TempAudioDirectory + "/Temp.mus"))
                    {

                    }

                    File.Copy(FileOpen[i], TempAudioDirectory + "/Temp.wav", true);


                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = false;
                    cmd.StartInfo.CreateNoWindow = false;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    FileInfo f = new FileInfo(TempAudioDirectory);
                    string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

                    cmd.StandardInput.WriteLine(drive);
                    cmd.StandardInput.WriteLine("cd " + TempAudioDirectory + "/TempAudio");
                    cmd.StandardInput.WriteLine("sx.exe -ps2stream -mt_blk -playlocmaincpu -removeuserall Temp.wav -=Temp.mus -v3");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();


                    HolderPaths.Add(TempAudioDirectory + "/Holder/" + $"{i:000}" + ".Mus");
                    File.Copy(TempAudioDirectory+"/Temp.mus", TempAudioDirectory+"/Holder/" + $"{i:000}" + ".Mus", true);
                }
                long CurrentOffset = 0;
                //Recalculate Offsets
                for (int i = 0; i < HolderPaths.Count; i++)
                {
                    var TempHolder = File.Open(HolderPaths[i], FileMode.Open);
                    var TempHdrHeader = hdrHandler.fileHeaders[i];
                    TempHolder.Position = TempHolder.Length;
                    StreamUtil.AlignBy(TempHolder, 0x100 * (hdrHandler.AligmentSize + 1));
                    long FixedLength = TempHolder.Position;
                    TempHolder.Close();
                    TempHdrHeader.OffsetInt = (int)(CurrentOffset / (0x100 * (hdrHandler.AligmentSize + 1)));
                    CurrentOffset += FixedLength;

                    hdrHandler.fileHeaders[i] = TempHdrHeader;

                }



                for (int i = 0; i < HolderPaths.Count; i++)
                {
                    using (Stream stream1 = File.Open(HolderPaths[i], FileMode.Open))
                    {
                        stream.Position = (hdrHandler.fileHeaders[i].OffsetInt * 0x100) / (hdrHandler.AligmentSize + 1);

                        StreamUtil.WriteStreamIntoStream(stream, stream1);
                    }

                    File.Delete(HolderPaths[i]);
                }

            }



            Directory.Delete(TempAudioDirectory, true);

            return hdrHandler;
        }

        public HDRHandler GenerateDATAndHDR3(string[] FileOpen, string FileSave, HDRHandler hdrHandler, string SXDirectory, string TempDirectory)
        {
            string TempAudioDirectory = TempDirectory + "/TempAudio";

            Directory.CreateDirectory(TempAudioDirectory);
            Directory.CreateDirectory(TempAudioDirectory + "/Holder");
            File.Copy(SXDirectory + "/sx_2002.exe", TempDirectory + "/sx.exe", true);

            if (File.Exists(FileSave))
            {
                File.Delete(FileSave);
            }
            while (File.Exists(FileSave))
            {

            }
            var file = File.Create(FileSave);
            while (!File.Exists(FileSave))
            {

            }
            file.Close();

            List<string> HolderPaths = new List<string>();
            //Create File and memory stream
            using (Stream stream = File.Open(FileSave, FileMode.Open))
            {
                for (int i = 0; i < FileOpen.Length; i++)
                {
                    //Copy File
                    if (File.Exists(TempAudioDirectory+"/Temp.mus"))
                    {
                        File.Delete(TempAudioDirectory + "/Temp.mus");
                    }

                    while (File.Exists(TempAudioDirectory+"/Temp.mus"))
                    {

                    }

                    File.Copy(FileOpen[i], TempAudioDirectory + "/Temp.wav", true);


                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = false;
                    cmd.StartInfo.CreateNoWindow = false;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    FileInfo f = new FileInfo(TempAudioDirectory);
                    string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

                    cmd.StandardInput.WriteLine(drive);
                    cmd.StandardInput.WriteLine("cd " + TempAudioDirectory);
                    cmd.StandardInput.WriteLine("sx.exe -ps2stream -eaxa_blk -playlocmaincpu -removeuserall Temp.wav -=Temp.mus -v3");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();


                    HolderPaths.Add(TempAudioDirectory+"/Holder/" + $"{i:000}" + ".Mus");
                    File.Copy(TempAudioDirectory+"/Temp.mus", TempAudioDirectory+"/Holder/" + $"{i:000}" + ".Mus", true);
                }
                long CurrentOffset = 0;
                //Recalculate Offsets
                for (int i = 0; i < HolderPaths.Count; i++)
                {
                    var TempHolder = File.Open(HolderPaths[i], FileMode.Open);
                    var TempHdrHeader = hdrHandler.fileHeaders[i];
                    TempHolder.Position = TempHolder.Length;
                    StreamUtil.AlignBy(TempHolder, 0x100 * (hdrHandler.AligmentSize + 1));
                    long FixedLength = TempHolder.Position;
                    TempHolder.Close();
                    TempHdrHeader.OffsetInt = (int)(CurrentOffset / (0x100 * (hdrHandler.AligmentSize + 1)));
                    CurrentOffset += FixedLength;

                    hdrHandler.fileHeaders[i] = TempHdrHeader;

                }



                for (int i = 0; i < HolderPaths.Count; i++)
                {
                    using (Stream stream1 = File.Open(HolderPaths[i], FileMode.Open))
                    {
                        stream.Position = (hdrHandler.fileHeaders[i].OffsetInt * 0x100) / (hdrHandler.AligmentSize + 1);

                        StreamUtil.WriteStreamIntoStream(stream, stream1);
                    }

                    File.Delete(HolderPaths[i]);
                }

            }



            Directory.Delete(TempAudioDirectory, true);

            return hdrHandler;
        }
    }
}
