using SSX_Library.Internal.Audio;
using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
    /// <remarks>This class's objects must be disposed of after
    /// opening to commit changes and clear temp files.</remarks>
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
            Directory.EnumerateFiles(soundPacksFolder, "*head*.big", SearchOption.AllDirectories)
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
    /// Gets a list of valid and invalid sound pack names based on if there is a corresponding .dat file to the .hdr
    /// </summary>
    public SoundPackName[] GetSoundPacks()
    {
        // Gets all the .hdr and .dat files. Get their path and filename.
        var hdrPaths = Directory.EnumerateFiles(_extractedHeaderFileFolder, "*.hdr", SearchOption.AllDirectories).ToArray();
        var datPaths = Directory.EnumerateFiles(_soundPacksFolder, "*.dat", SearchOption.AllDirectories).ToArray();
        var hdrNames = hdrPaths.Select(n => Path.GetFileNameWithoutExtension(n)).ToArray();
        var datNames = datPaths.Select(n => Path.GetFileNameWithoutExtension(n)).ToArray();

        // Confirm names are unique.
        Debug.Assert(
            hdrNames.Distinct().Count() == hdrNames.Length,
            "Duplicate .hdr file names found. Please report this bug or make sure you didnt tamper with the temp folder."
        );
       Debug.Assert(
            datNames.Distinct().Count() == datNames.Length,
            "Duplicate .dat file names found. Please report this bug or make sure you didnt duplicate the .dat files yourselve."
        );
        
    // Create SoundPackName array
        var nameToDatPathLookup = datPaths.ToDictionary(p => Path.GetFileNameWithoutExtension(p) ?? "", p => p);
        var soundPackNames = hdrPaths.Select(hdrPath => 
        {
            // For each header path, check if the name exists on the lookup table.
            // If it's found then set to valid and set the datPath to it. If not found then
            // set as invalid and set the datPath as empty.
            var hdrName = Path.GetFileNameWithoutExtension(hdrPath);
            nameToDatPathLookup.TryGetValue(hdrName, out var foundDatPath);
            return new SoundPackName(foundDatPath != null, hdrName, hdrPath, foundDatPath ?? "");
        });
        return [.. soundPackNames];
    }

    /// <summary>
    /// Get the number of sounds in a sound pack.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int GetSoundPackSoundCount(SoundPackName soundPackName)
    {
        // Load the .hdr
        if (!File.Exists(soundPackName.HdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + soundPackName.HdrPath);
        }

        // Read and return the File count
        var hdr = new HDR();
        hdr.Load(soundPackName.HdrPath);
        return hdr.FileCount;
    }

    /// <summary>
    /// Get the EventID of a sound.
    /// </summary>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public byte GetSoundPackEventID(SoundPackName soundPackName, int soundID)
    {
        // Load the .hdr
        if (!File.Exists(soundPackName.HdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + soundPackName.HdrPath);
        }

        // Read and return the sound event ID
        var hdr = new HDR();
        hdr.Load(soundPackName.HdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{soundPackName.HdrPath}'s entry type does not contain Event IDs ");
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
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public byte GetSoundPackSpeakerID(SoundPackName soundPackName, int soundID)
    {
        // Load the .hdr
        if (!File.Exists(soundPackName.HdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + soundPackName.HdrPath);
        }

        // Read and return the sound speaker ID
        var hdr = new HDR();
        hdr.Load(soundPackName.HdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{soundPackName.HdrPath}'s entry type does not contain Speaker IDs ");
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
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public void SetSoundPackEventID(SoundPackName soundPackName, int soundID, byte newEventID)
    {
        // Load the .hdr
        if (!File.Exists(soundPackName.HdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + soundPackName.HdrPath);
        }

        // Update the EventID and save back to disk
        var hdr = new HDR();
        hdr.Load(soundPackName.HdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{soundPackName.HdrPath}'s entry type does not contain Event IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Event ID is out of range");
        }
        var newHeader = hdr.FileHeaders[soundID];
        newHeader.EventID = newEventID;
        hdr.FileHeaders[soundID] = newHeader;
        hdr.Save(soundPackName.HdrPath);
    }

    /// <summary>
    /// Set the SpeakerID of a sound.
    /// </summary>
    /// <param name="soundID"> A sound ID. It must be lower than the amount of sounds in a sound pack. </param>
    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public void SetSoundPackSpeakerID(SoundPackName soundPackName, int soundID, byte newSpeakerID)
    {
        // Load the .hdr
        if (!File.Exists(soundPackName.HdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + soundPackName.HdrPath);
        }

        // Update the SpeakerID and save back to disk
        var hdr = new HDR();
        hdr.Load(soundPackName.HdrPath);
        if (hdr.EntryTypes is 0 or 1){
            throw new InvalidOperationException($"{soundPackName.HdrPath}'s entry type does not contain Speaker IDs ");
        }
        if (soundID >= hdr.FileCount)
        {
            throw new IndexOutOfRangeException("Speaker ID is out of range");
        }
        var newHeader = hdr.FileHeaders[soundID];
        newHeader.SpeakerID = newSpeakerID;
        hdr.FileHeaders[soundID] = newHeader;
        hdr.Save(soundPackName.HdrPath);
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

                // Turn the .mus file to .wav, and place it on the extraction folder.
                var cmd = new Process();
                cmd.StartInfo.FileName = OperatingSystem.IsLinux() ? "/bin/bash" : "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.Start();
                var wine = OperatingSystem.IsLinux() ? "WINEDEBUG=-all wine " : "";
                var outputPath = Path.Join(folderToExtractTo, Path.GetFileName(soundPackName) + $"_{i:000}" + ".wav");
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
    /// Updates a sound pack with .wav sounds from a list of .wav files.
    /// </summary>
    /// <remarks>
    /// The order of the wavFilePaths items matter.<br></br>
    /// Length of the array must match the number of sounds in the pack.<br></br>
    /// Adding individual sounds is not supported, You have to extract the whole sound pack,
    /// replace the sounds you want to change, and then rebuild again.
    /// </remarks>
    public void WavFilesToSoundPack(string soundPackName, string[] wavFilePaths, bool useSsx3Flag = false)
    {
        // Load the .hdr
        var hdrPath = Path.Join(_extractedHeaderFileFolder, soundPackName + ".hdr");
        if (!File.Exists(hdrPath))
        {
            throw new FileNotFoundException("Could not find header file: " + hdrPath);
        }
        var hdr = new HDR();
        hdr.Load(hdrPath);
        if (hdr.FileCount != wavFilePaths.Length)
        {
            throw new IndexOutOfRangeException("wavFilePaths length does not match the sound count.");
        }
        
        var musFilePaths = new List<string>();
        try
        {
            // Turn the .wav's to .mus's and store their paths in musFilePaths.
            foreach (var wavPath in wavFilePaths)
            {
                var tempMusFilePath = Path.GetTempFileName();
                musFilePaths.Add(tempMusFilePath);
                var cmd = new Process();
                cmd.StartInfo.FileName = OperatingSystem.IsLinux() ? "/bin/bash" : "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.Start();
                var wine = OperatingSystem.IsLinux() ? "WINEDEBUG=-all wine " : "";
                var sxFlag = useSsx3Flag ? "-eaxa_blk" : "-mt_blk";
                cmd.StandardInput.WriteLine($"{wine}{_sx_2002Path} -ps2stream {sxFlag} -playlocmaincpu -removeuserall {wavPath} -={tempMusFilePath} -v3");
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();
            }

            // Update the .hdr to the new offsets.
            long currentOffset = 0;
            for (int i = 0; i < musFilePaths.Count; i++)
            {
                // Get the size of the .mus while aligned.
                using var musFile = File.OpenRead(musFilePaths[i]);
                musFile.Seek(0, SeekOrigin.End);
                musFile.AlignBy(0x100 * (hdr.AligmentSize + 1));

                // Update the header's offset
                var header = hdr.FileHeaders[i];
                header.Offset = (int)currentOffset / (0x100 * hdr.AligmentSize + 1);
                hdr.FileHeaders[i] = header;

                // Advance offset by the size
                currentOffset += musFile.Length;
            }

            // Update the .dat.
            using var datFile = File.Create(Path.Join(_soundPacksFolder, soundPackName + ".dat"));
            for (int i = 0; i < musFilePaths.Count; i++)
            {
                // Copy the whole .mus into the .dat with the corresponding alignment. 
                using var musFile = File.OpenRead(musFilePaths[i]);
                datFile.Position = hdr.FileHeaders[i].Offset * 0x100 * (hdr.AligmentSize + 1);
                musFile.CopyTo(datFile);
            }
        }
        finally
        {
            foreach (var path in musFilePaths)
            {
                File.Delete(path);
            }
        }
        hdr.Save(hdrPath);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Archive the temp folder back to its .big path. And delete the temp folder.
        BIG.Create(_headerBigType, _extractedHeaderFileFolder, _headerFilePath, false);
        Directory.Delete(_extractedHeaderFileFolder, true);

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public record SoundPackName(bool Valid, string Name, string HdrPath, string DatPath);
}
