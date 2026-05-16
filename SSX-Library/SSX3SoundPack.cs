using SSX_Library;
using SSX_Library.Internal.Utilities;
using SSXLibrary.FileHandlers;
using System.Diagnostics;
using System.IO.Hashing;

namespace SSXLibrary
{
    public class SSX3SoundPack
    {
        public static async Task FullExtractAsync(string MainBig, string ExtractFolder, string SXDirectory)
        {
            Console.WriteLine("Starting Audio Extract");
            //Extract Mainbig to the temp folder
            string HiddenFolder = ExtractFolder + "\\OriginalData";
            string HDRFolder = HiddenFolder + "\\HDRFolder";
            string DATFolder = HiddenFolder + "\\DATFolder";
            string MUSFolder = HiddenFolder + "\\MUSFolder";

            DirectoryInfo di = Directory.CreateDirectory(HiddenFolder);
            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            Console.WriteLine("Creating Hidden Directories");

            Directory.CreateDirectory(HDRFolder);
            Directory.CreateDirectory(DATFolder);
            Directory.CreateDirectory(MUSFolder);

            Console.WriteLine("Extracting Main Big");

            BIG.Extract(MainBig, DATFolder);

            string[] LangHeader = Directory.GetFiles(DATFolder, "*.big", SearchOption.AllDirectories);

            if (LangHeader.Length != 1)
            {
                //MessageBox.Show("Incorrect Ammount of Bigs");
                return;
            }

            Console.WriteLine("Extracting HDR Big");

            BIG.Extract(LangHeader[0], HDRFolder);

            File.Copy(SXDirectory + "/sx_2002.exe", MUSFolder + "/sx.exe", true);

            //Extract DATS to Correct Folders
            string[] DATs = Directory.GetFiles(DATFolder, "*.DAT", SearchOption.AllDirectories);

            Console.WriteLine("Starting DAT To WAV Conversion");
            Console.WriteLine("Using " + maxProcesses.ToString() + " Threads");

            List<Task> tasks = new List<Task>();

            for (int i = 0; i < DATs.Length; i++)
            {
                tasks.Add(ExtractPrcoess(DATs[i], MUSFolder, ExtractFolder));
            }

            await Task.WhenAll(tasks);
        }

        public static int maxProcesses = Environment.ProcessorCount; // <-- limit concurrency
        static SemaphoreSlim semaphore = new SemaphoreSlim(maxProcesses);

        static async Task ExtractPrcoess(string DATFile, string MUSFolder, string ExtractFolder)
        {
            await semaphore.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    Console.WriteLine("Extracting DAT TO MUS - " + MUSFolder);

                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    FileInfo f = new FileInfo(MUSFolder);
                    string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

                    cmd.StandardInput.WriteLine(drive);
                    cmd.StandardInput.WriteLine("cd " + MUSFolder);

                    string MUSFolderName = DATFile.Replace(".dat", "").Replace("DATFolder", "MUSFolder");
                    string ExtractMUSFolder = MUSFolderName.Replace("OriginalData\\MUSFolder", "");

                    Directory.CreateDirectory(MUSFolderName);
                    List<string> MUSFiles = new List<string>();

                    using (Stream stream = File.Open(DATFile, FileMode.Open))
                    {
                        List<long> Offsets = new List<long>();

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

                        for (int a = 0; a < Offsets.Count; a++)
                        {
                            stream.Position = Offsets[a];

                            long ByteSize = 0;

                            if (a == Offsets.Count - 1)
                            {
                                ByteSize = stream.Length - Offsets[a];
                            }
                            else
                            {
                                ByteSize = Offsets[a + 1] - Offsets[a];
                            }

                            MemoryStream StreamMemory = new MemoryStream();

                            byte[] Data = StreamUtil.ReadBytes(stream, (int)ByteSize);

                            StreamUtil.WriteBytes(StreamMemory, Data);

                            string NewPath = MUSFolderName + $"\\{a:000}.mus";

                            MUSFiles.Add(NewPath);

                            Console.WriteLine(a.ToString() + "/" + Offsets.Count + " Creating MUS File - " + NewPath);

                            var file = File.Create(NewPath);
                            StreamMemory.Position = 0;
                            StreamMemory.CopyTo(file);
                            StreamMemory.Dispose();
                            file.Close();
                        }
                    }

                    Console.WriteLine("Starting MUS TO WAV CONVERSION - " + ExtractMUSFolder);

                    Directory.CreateDirectory(ExtractMUSFolder);

                    string[] HASHText = new string[MUSFiles.Count];

                    //Extract to WAVs
                    for (int j = 0; j < MUSFiles.Count; j++)
                    {
                        string LoadPath = MUSFiles[j].Replace(MUSFolder, "");
                        string ExtractPath = ExtractFolder + LoadPath.Replace(".mus", ".wav");

                        Console.WriteLine(j.ToString() + "/" + MUSFiles.Count + " Starting Conversion - " + ExtractPath);
                        cmd.StandardInput.WriteLine("sx.exe -wave -s16l_int -playlocmaincpu  \"" + MUSFiles[j] + "\" -=\"" + ExtractPath + "\"");
                        string marker = "__DONE__";
                        cmd.StandardInput.WriteLine($"echo {marker}");

                        // Read output until the marker appears
                        string line;
                        while ((line = cmd.StandardOutput.ReadLine()) != null)
                        {
                            if (line.Trim() == marker)
                                break;
                        }

                        //Generate HASH for WAVs and save next to MUS
                        ulong Data = XxHash64.HashToUInt64(File.ReadAllBytes(ExtractPath));

                        HASHText[j] = Data.ToString();
                    }

                    File.WriteAllLines(MUSFolderName + "\\WAVHashSet.Hash", HASHText);

                    Console.WriteLine("Pending Window Finish");

                    cmd.StandardInput.WriteLine("exit");
                    cmd.WaitForExit();
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static void FullRebuild(string MainFolder, string MainBig, string SXDirectory)
        {
            //Extract Mainbig to the temp folder
            string HiddenFolder = MainFolder + "\\OriginalData";
            string HDRFolder = HiddenFolder + "\\HDRFolder";
            string DATFolder = HiddenFolder + "\\DATFolder";
            string MUSFolder = HiddenFolder + "\\MUSFolder";

            File.Copy(SXDirectory + "/sx_2002.exe", MUSFolder + "/sx.exe", true);

            //Get all HDR files
            string[] HDRFiles = Directory.GetFiles(HDRFolder, "*.hdr", SearchOption.AllDirectories);

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            FileInfo f = new FileInfo(MUSFolder);
            string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

            cmd.StandardInput.WriteLine(drive);
            cmd.StandardInput.WriteLine("cd " + MUSFolder);

            //Do a quick verify that original data hasnt been messed with
            //Verify big file exists in DATFolder
            //Verify Folders match between HDR, MUS and Main set of data



            //Using each HDR file indivdually check each folder and there are matching wavs to confirm hash matches
            //If not matching update with new hash and convert Wav to Mus
            //Mark File as needing rebuild
            //Once entire file checked if marked rebuild file into single DAT File
            for (int i = 0; i < HDRFiles.Length; i++)
            {
                bool Updated = false;

                string HDRFile = HDRFiles[i];
                string DATFile = HDRFiles[i].Replace("HDRFolder", "DATFolder").Replace(".hdr", ".dat");
                string HDRMusFile = HDRFile.Replace("HDRFolder", "MUSFolder").Replace(".hdr","");
                string MainWavFolder = HDRMusFile.Replace("OriginalData\\MUSFolder\\", "");

                string[] HashFiles = Directory.GetFiles(HDRMusFile, "*.hash", SearchOption.AllDirectories);
                string[] WAVFiles = Directory.GetFiles(MainWavFolder, "*.wav", SearchOption.AllDirectories);
                string[] MUSFiles = Directory.GetFiles(HDRMusFile, "*.mus", SearchOption.AllDirectories);

                string[] Hash = File.ReadAllLines(HashFiles[0]);
                string[] HASHText = new string[WAVFiles.Length];
                //May need to add alphabetaical sort for stinky linux users to ensure it doesnt die

                for (int j = 0; j < WAVFiles.Length; j++)
                {
                    //Generate HASH for WAVs and save next to MUS
                    ulong Data = XxHash64.HashToUInt64(File.ReadAllBytes(WAVFiles[j]));

                    HASHText[j] = Data.ToString();

                    if (HASHText[j] != Hash[j])
                    {
                        cmd.StandardInput.WriteLine("sx.exe -ps2stream -eaxa_blk -playlocmaincpu -removeuserall \"" + WAVFiles[j] + "\" -=\"" + MUSFiles[j] + "\" -v3");

                        Updated = true;
                    }
                }

                //Rebuild DAT File if changed
                if (Updated)
                {
                    File.WriteAllLines(HDRMusFile + "\\WAVHashSet.Hash", HASHText);

                    string marker = i.ToString();
                    cmd.StandardInput.WriteLine($"echo {marker}");

                    // Read output until the marker appears
                    string line;
                    while ((line = cmd.StandardOutput.ReadLine()) != null)
                    {
                        if (line.Trim() == marker)
                            break;
                    }

                    long CurrentOffset = 0;

                    if (File.Exists(DATFile))
                    {
                        File.Delete(DATFile);
                    }
                    while (File.Exists(DATFile))
                    {

                    }

                    using (Stream stream = File.Create(DATFile))
                    {
                        HDRHandler hdrHandler = new HDRHandler();
                        hdrHandler.Load(HDRFile);

                        //Recalculate Offsets
                        for (int a = 0; a < MUSFiles.Length; a++)
                        {
                            var TempHolder = File.Open(MUSFiles[a], FileMode.Open);

                            var TempHdrHeader = hdrHandler.fileHeaders[a];
                            TempHolder.Position = TempHolder.Length;

                            StreamUtil.AlignBy(TempHolder, 0x100 * (hdrHandler.LongFileMode + 1));
                            long FixedLength = TempHolder.Position;
                            TempHolder.Close();
                            TempHdrHeader.OffsetInt = (int)(CurrentOffset / (0x100 * (hdrHandler.LongFileMode + 1)));
                            CurrentOffset += FixedLength;

                            hdrHandler.fileHeaders[a] = TempHdrHeader;

                            using (Stream stream1 = File.Open(MUSFiles[a], FileMode.Open))
                            {
                                stream.Position = (hdrHandler.fileHeaders[a].OffsetInt * 0x100) * (hdrHandler.LongFileMode + 1);

                                StreamUtil.WriteStreamIntoStream(stream, stream1);
                            }

                        }
                        
                        hdrHandler.Save(HDRFile);
                    }
                }
            }

            string[] BigFile = Directory.GetFiles(DATFolder, "*.big", SearchOption.AllDirectories);

            BIG.Create(BigType.BIG4, HDRFolder, BigFile[0], false, true);
            BIG.Create(BigType.BIG4, DATFolder, MainBig, false, true);

            cmd.StandardInput.Close();
            cmd.WaitForExit();
        }
    }
}
