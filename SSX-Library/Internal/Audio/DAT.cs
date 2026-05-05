using SSX_Library.Internal.Utilities;
using System.Diagnostics;

namespace SSX_Library.Internal.Audio;

public static partial  class DAT
{
    public static void Extract(string audioToolsFolder, string datPath, string outputFolder)
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var sx_2002Path = Path.Combine(audioToolsFolder, "sx_2002.exe");
        if (!File.Exists(sx_2002Path))
        {
            throw new FileNotFoundException("Could not find sx_2002.exe on the provided folder");
        }
        
        var platform = Compatibility.GetPlatform();
        if (platform == Compatibility.Platform.Linux && !File.Exists("/bin/wine"))
        {
            throw new FileNotFoundException("Wine must be installed on your linux machine");
        }

        using var datFile = File.OpenRead(datPath);
        var offsets = new List<long>();
        while (true)
        {
            var offset = ByteConv.FindBytePattern(datFile, [ 0x53, 0x43, 0x48, 0x6C ]);
            if (offset == -1) break;
            offsets.Add(offset);
        }

        for (int i = 0; i < offsets.Count; i++)
        {
            datFile.Position = offsets[i];
            long byteSize = 0;
            if (i == offsets.Count - 1)
            {
                byteSize = datFile.Length - offsets[i];
            }
            else
            {
                byteSize = offsets[i + 1] - offsets[i];
            }
            using var tempMusFile = File.OpenWrite(Path.Combine(tempDir.FullName, "temp.mus"));
            var offsetData = new byte[byteSize];
            datFile.ReadExactly(offsetData);
            tempMusFile.Write(offsetData);

            var cmd = new Process();
            cmd.StartInfo.FileName = platform switch
            {
                Compatibility.Platform.Windows => "cmd.exe",
                Compatibility.Platform.Linux => "/bin/bash",
                _ => "",
            };
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.Start();

            string wine = platform == Compatibility.Platform.Windows? "" : "wine ";
            string outputPath = Path.Combine(outputFolder, $"{i:000}" + ".wav");
            cmd.StandardInput.WriteLine("cd " + tempDir.FullName);
            cmd.StandardInput.WriteLine($"{wine}sx_2002.exe -wave -s16l_int -playlocmaincpu  Temp.mus -={outputPath}");
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();
        }
        tempDir.Delete(true);
    }
}