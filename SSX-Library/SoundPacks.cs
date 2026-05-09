using SSX_Library.Internal.Utilities;


namespace SSX_Library;

/*
    For now:
    I'll not add a feature to add individual sounds, You have to extract the whole sound pack,
    replace the one you want to change, and then rebuild again.

    Warning:
    A folder containing.dat files was found right next to a headers.big. This is in ssx Tricky's 
    data/speech/anim
    The headers.big also replicate the fact that the .dat is in a folder by the same name, interesting. I think this is because without archiving, the hdr files are meant to be right next to the dat files

*/

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
        _sx_2002Path = Path.Combine(audioToolsFolder, "sx_2002.exe");
        _sx_2004Path = Path.Combine(audioToolsFolder, "sx_2004.exe");
        if (!File.Exists(_sx_2002Path) || !File.Exists(_sx_2004Path))
        {
            throw new FileNotFoundException("Could not find required audio tools in the provided folder");
        }
        if (OperatingSystem.IsLinux() && !File.Exists("/bin/wine"))
        {
            throw new FileNotFoundException("Wine must be installed on your linux machine");
        }

        // Validate the soundPacksFolder
        _headerFilePath = "";
        foreach (var file in Directory.GetFiles(soundPacksFolder))
        {
            // Find the header file
            var fileName = Path.GetFileName(file);
            if (fileName.Contains("header") && fileName.EndsWith(".big"))
            {
                _headerFilePath = file;
                break;
            }
        }
        if (_headerFilePath == "")
        {
            throw new FileNotFoundException("Header file not found");
        }
        
        // Extract the headers.big into a temp folder.
        _extractedHeaderFileFolder = Directory.CreateTempSubdirectory().FullName;
        _headerBigType = BIG.GetBigType(_headerFilePath);
        BIG.Extract(_headerFilePath, _extractedHeaderFileFolder);
    }

    /// <summary>
    /// Gets the list of packs only if there is a corresponding .dat file to the .hdr
    /// </summary>
    /// <returns>A list of sound pack names</returns>
    public string[] GetValidSoundPacks()
    {
        return ["sus"];
    }

    public int GetSoundPackSoundCount(string soundPackName)
    {
        return 0;
    }

    public byte GetSoundPackEventID(string soundPackName, int soundID)
    {
        return 0;
    }

    public byte GetSoundPackSpeakerID(string soundPackName, int soundID)
    {
        return 0;
    }

    public void SetSoundPackEventID(string soundPackName, int soundID, byte eventID)
    {

    }

    public void SetSoundPackSpeakerID(string soundPackName, int soundID, byte speakerID)
    {

    }

    // Make sure the extracted wav file is named after it's pack name and sound ID
    public void ExtractSoundPack(string soundPackName, string folderToExtractTo)
    {
        
    }

    /// <summary>
    /// Note: Order of the wavFilePaths array matter.
    /// Length of the array must match the number of sounds in the pack.
    /// </summary>
    public void ReplaceSoundPackWithWavFolder(string soundPackName, string[] wavFilePaths)
    {
        
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
