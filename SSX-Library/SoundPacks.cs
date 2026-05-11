using SSX_Library.Internal.Audio;
using SSX_Library.Internal.Utilities;
using System.Diagnostics;

namespace SSX_Library;

/// <summary>
/// Represents a folder containing sound packs, where each pack holds sounds.
/// </summary>
/// <remarks>
/// A valid folder is considered a SoundPack if it has the following:<br></br>
/// - A .big file with the word "header" in it's name. <br></br>
/// - The header archive must contains .hdr files inside it. <br></br>
/// - .dat files, or folders which contain .dat files.<br></br>
/// Proprietary tools are required to use this class.<br></br>
/// This class's objects must be disposed of after opening - to commit changes to the filesystem.
/// </remarks>
public sealed class SoundPacks : IDisposable
{
    private bool _disposed = false;
    private readonly string _sx_2002Path;
    private readonly string _sx_2004Path;
    private readonly string _soundPacksFolder;
    private readonly string _headerFilePath;
    private readonly string _extractedHeaderFileFolder;
    private readonly BigType _headerBigType;

    /// <summary>
    /// Create a SoundsPacks folder handler.
    /// </summary>
    /// <param name="audioToolsFolder"> The path to the proprietary EA audio tools, 
    /// used for sound extraction/generation.</param>
    public SoundPacks(string soundPacksFolder, string audioToolsFolder)
    {
        // Validate the tools
        _sx_2002Path = Path.Join(audioToolsFolder, "sx_2002.exe");
        _sx_2004Path = Path.Join(audioToolsFolder, "sx_2004.exe");
        if (!File.Exists(_sx_2002Path) || !File.Exists(_sx_2004Path))
        {
            throw new FileNotFoundException("Could not find required audio tools in the provided folder");
        }
        if (OperatingSystem.IsLinux() && !File.Exists("/bin/wine"))
        {
            throw new FileNotFoundException("Wine must be installed on your linux machine");
        }

        // Validate the soundPacksFolder
        _soundPacksFolder = soundPacksFolder;
        _headerFilePath =
            Directory.EnumerateFiles(soundPacksFolder, "*header*.big", SearchOption.AllDirectories)
            .FirstOrDefault("");
        if (_headerFilePath == "")
        {
            throw new FileNotFoundException("Header file not found");
        }
        
        // Extract the header .big into a temp folder.
        _extractedHeaderFileFolder = Directory.CreateTempSubdirectory().FullName;
        _headerBigType = BIG.GetBigType(_headerFilePath);
        BIG.Extract(_headerFilePath, _extractedHeaderFileFolder);
    }

    /// <summary>
    /// Gets two lists of packs based on if there is a corresponding .dat file to the .hdr
    /// </summary>
    /// <returns>A tuple with lists of valid and invalid sound pack names.
    /// The names are paths relative to the SoundPacks but without
    /// the extension. e.g. "/char/talk", that way its can be used for searching for both the hdr and dat.</returns>
    public (string[] valid, string[] invalid) GetSoundPacks()
    {
        // Gets all the .hdr files, relative to the temp folder.
        var headerPaths = 
            Directory.EnumerateFiles(_extractedHeaderFileFolder, "*.hdr", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_extractedHeaderFileFolder, p)) // Remove the folder path
            .Select(p => Path.ChangeExtension(p, null)); // Remove the extension
        
        // Gets all the .dat files, relative to the sound packs folder.
        var dataPaths =
            Directory.EnumerateFiles(_soundPacksFolder, "*.dat", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_soundPacksFolder, p)) // Remove the folder path
            .Select(p => Path.ChangeExtension(p, null)); // Remove the extension

        // If a .dat exists for an .hdr then it's valid, If not then its invalid.
        var soundPacks = headerPaths.ToLookup(p => dataPaths.Contains(p));
        return ([..soundPacks[true]], [..soundPacks[false]]);
    }

    /// <summary>
    /// Get the number of sounds in a sound pack.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    public int GetSoundPackSoundCount(string soundPackName)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }

        // Read and return the File count
        var hdr = new HDR();
        hdr.Load(hdrPath);
        return hdr.FileCount;
    }

    /// <summary>
    /// Get the EventID of a sound.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    public byte GetSoundPackEventID(string soundPackName, int soundID)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }

        // Read and return the sound event ID
        var hdr = new HDR();
        hdr.Load(hdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{hdrPath}'s entry type does not contain Event IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Sound ID is out of range");
        }
        return hdr.FileHeaders[soundID].EventID;
    }

    /// <summary>
    /// Get the SpeakerID of a sound.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    public byte GetSoundPackSpeakerID(string soundPackName, int soundID)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }

        // Read and return the sound speaker ID
        var hdr = new HDR();
        hdr.Load(hdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{hdrPath}'s entry type does not contain Speaker IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Speaker ID is out of range");
        }
        return hdr.FileHeaders[soundID].SpeakerID;
    }

    /// <summary>
    /// Set the EventID of a sound.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    public void SetSoundPackEventID(string soundPackName, int soundID, byte newEventID)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }

        // Update the EventID and save back to disk
        var hdr = new HDR();
        hdr.Load(hdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{hdrPath}'s entry type does not contain Event IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Event ID is out of range");
        }
        var newHeader = hdr.FileHeaders[soundID];
        newHeader.EventID = newEventID;
        hdr.FileHeaders[soundID] = newHeader;
        hdr.Save(hdrPath);
    }

    /// <summary>
    /// Set the SpeakerID of a sound.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    public void SetSoundPackSpeakerID(string soundPackName, int soundID, byte newSpeakerID)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }

        // Update the SpeakerID and save back to disk
        var hdr = new HDR();
        hdr.Load(hdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{hdrPath}'s entry type does not contain Speaker IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Speaker ID is out of range");
        }
        var newHeader = hdr.FileHeaders[soundID];
        newHeader.SpeakerID = newSpeakerID;
        hdr.FileHeaders[soundID] = newHeader;
        hdr.Save(hdrPath);
    }

    /// <summary>
    /// Extracts a sound pack to a folder. The output files will be .wav, each named by the
    /// sound pack name plus the ID of the sound.
    /// </summary>
    /// <param name="soundPackName"> A valid sound pack name, obtainable through GetSoundPacks() </param>
    public void ExtractSoundPack(string soundPackName, string folderToExtractTo)
    {
        // Validate the tools again (to be safe)
        if (!File.Exists(_sx_2002Path))
        {
            throw new FileNotFoundException("Could not find required audio tools in the provided folder");
        }
        if (OperatingSystem.IsLinux() && !File.Exists("/bin/wine"))
        {
            throw new FileNotFoundException("Wine must be installed on your linux machine");
        }

        using var datFile = File.OpenRead(Path.Join(_extractedHeaderFileFolder, soundPackName + ".dat"));

        // Look for offsets based on the music file's header magic signature.
        var offsets = new List<long>();
        while (true)
        {
            var offset = ByteConv.FindBytePattern(datFile, [ 0x53, 0x43, 0x48, 0x6C ]);
            if (offset == -1) break;
            offsets.Add(offset);
        }

        for (int i = 0; i < offsets.Count; i++)
        {
            // Create a temp .mus file
            datFile.Position = offsets[i];
            long musFileSize;
            if (i == offsets.Count - 1)
            {
                musFileSize = datFile.Length - offsets[i];
            }
            else
            {
                musFileSize = offsets[i + 1] - offsets[i];
            }
            var tempMusFilePath = Path.GetTempFileName();
            try
            {
                using var tempMusFile = File.OpenWrite(tempMusFilePath);
                datFile.CopyTo(tempMusFile, (int)musFileSize);

                // Take the mus file as input, and output the sound to the extraction folder as .wav.
                var cmd = new Process();
                cmd.StartInfo.FileName = OperatingSystem.IsLinux() ? "/bin/bash" : "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.Start();
                var wine = OperatingSystem.IsLinux() ? "WINEDEBUG=-all wine " : "";
                var outputPath = Path.Join(folderToExtractTo, soundPackName + $"_{i:000}" + ".wav");
                cmd.StandardInput.WriteLine($"{wine}{_sx_2002Path} -wave -s16l_int -playlocmaincpu  {tempMusFilePath} -={outputPath}");
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();
            }
            finally
            {
                File.Delete(tempMusFilePath);
            }
        }
    }

    /// <summary>
    /// Note: Order of the wavFilePaths array matter.
    /// Length of the array must match the number of sounds in the pack.
    /// </summary>
    /// <remarks>
    /// Adding individual sounds is not supported, You have to extract the whole sound pack,
    /// replace the sounds you want to change, and then rebuild again.
    /// </remarks>
    public void ReplaceSoundPackWithWavFolder(string soundPackName, string[] wavFilePaths)
    {
        // Todo Next
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Archive the temp folder back to its .big path. And delete the temp folder.
        BIG.Create(_headerBigType, _extractedHeaderFileFolder, _headerFilePath, true);
        Directory.Delete(_extractedHeaderFileFolder, true);

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
