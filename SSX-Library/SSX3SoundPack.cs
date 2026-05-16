using ICSharpCode.SharpZipLib.Core;
using SSX_Library;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SSXLibrary
{
    public class SSX3SoundPack
    {

        public static void FullExtract(string MainBig, string ExtractFolder, string SXDirectory)
        {
            //Extract Mainbig to the temp folder
            string HiddenFolder = ExtractFolder + "\\OriginalData";
            string HDRFolder = HiddenFolder + "\\HDRFolder";
            string DATFolder = HiddenFolder + "\\DATFolder";
            string MUSFolder = HiddenFolder + "\\MUSFolder";

            Directory.CreateDirectory(HiddenFolder);
            Directory.CreateDirectory(HDRFolder);
            Directory.CreateDirectory(DATFolder);
            Directory.CreateDirectory(MUSFolder);

            BIG.Extract(MainBig, DATFolder);

            string[] LangHeader = Directory.GetFiles(DATFolder, "*.big", SearchOption.AllDirectories);

            if (LangHeader.Length != 1)
            {
                //MessageBox.Show("Incorrect Ammount of Bigs");
                return;
            }

            BIG.Extract(LangHeader[0], HDRFolder);

            File.Copy(SXDirectory + "/sx_2002.exe", MUSFolder + "/sx.exe", true);

            //Extract DATS to Correct Folders
            string[] DATs = Directory.GetFiles(DATFolder, "*.DAT", SearchOption.AllDirectories);

            for (int i = 0; i < DATs.Length; i++)
            {
                string MUSFolderName = DATs[i].Replace(".dat", "").Replace("DATFolder", "MUSFolder");
                string ExtractMUSFolder = MUSFolderName.Replace("OriginalData\\MUSFolder", "");

                Directory.CreateDirectory(MUSFolderName);
                List<string> MUSFiles = new List<string>();
                List<string> WavFiles = new List<string>();

                using (Stream stream = File.Open(DATs[i], FileMode.Open))
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

                        var file = File.Create(NewPath);
                        StreamMemory.Position = 0;
                        StreamMemory.CopyTo(file);
                        StreamMemory.Dispose();
                        file.Close();
                    }
                }

                Directory.CreateDirectory(ExtractMUSFolder);

                //Extract to WAVs
                for (int j = 0; j < MUSFiles.Count; j++)
                {
                    Process cmd = new Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = false;
                    cmd.StartInfo.CreateNoWindow = false;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.Start();

                    FileInfo f = new FileInfo(MUSFolder);
                    string drive = System.IO.Path.GetPathRoot(f.FullName.Substring(0, 2));

                    string LoadPath = MUSFiles[j].Replace(MUSFolder, "");
                    string ExtractPath = ExtractFolder +LoadPath.Replace(".mus", ".wav");

                    WavFiles.Add(ExtractPath);

                    cmd.StandardInput.WriteLine(drive);
                    cmd.StandardInput.WriteLine("cd " + MUSFolder);
                    cmd.StandardInput.WriteLine("sx.exe -wave -s16l_int -playlocmaincpu  \"" + MUSFiles[j] + "\" -=\"" + ExtractPath + "\"");
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();
                    cmd.WaitForExit();

                    //Generate HASH for WAVs and save next to MUS
                    using (SHA256 sha256Hash = SHA256.Create())
                    {
                        byte[] Data = new byte[1];
                        using (Stream stream = File.Open(ExtractPath, FileMode.Open))
                        {
                            Data = sha256Hash.ComputeHash(stream.ReadBytes((int)stream.Length));
                        }
                        // Create a new Stringbuilder to collect the bytes
                        // and create a string.
                        var sBuilder = new StringBuilder();

                        // Loop through each byte of the hashed data
                        // and format each one as a hexadecimal string.
                        for (int a = 0; a < Data.Length; a++)
                        {
                            sBuilder.Append(Data[a].ToString("x2"));
                        }

                        File.WriteAllText(MUSFiles[j].Replace(".mus", ".hash"), sBuilder.ToString());
                    }
                }
            }
        }
    }
}
