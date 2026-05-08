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
    private readonly string Sx_2002Path;
    private readonly string Sx_2004Path;
    private readonly string HeaderFilePath;
    private readonly string ExtractedHeaderFileFolder;
    private readonly BigType HeaderBigType;

    /// <summary>
    /// Create a SoundsPack folder handler.
    /// </summary>
    /// <param name="audioToolsFolder"> The path to the proprietary EA audio tools, 
    /// used for sound extraction/generation.</param>
    public SoundPacks(string soundPackFolder, string audioToolsFolder)
    {
        // Validate the tools
        Sx_2002Path = Path.Combine(audioToolsFolder, "sx_2002.exe");
        Sx_2004Path = Path.Combine(audioToolsFolder, "sx_2004.exe");
        if (!File.Exists(Sx_2002Path) || !File.Exists(Sx_2004Path))
        {
            throw new FileNotFoundException("Could not find sx_2002.exe on the provided folder");
        }
        else if (OperatingSystem.IsLinux() && !File.Exists("/bin/wine"))
        {
            throw new FileNotFoundException("Wine must be installed on your linux machine");
        }

        // Validate the soundPackFolder
        HeaderFilePath = "";
        var soundPackFiles = Directory.GetFiles(soundPackFolder);
        foreach (var file in soundPackFiles)
        {
            // Find the header file
            var fileName = Path.GetFileName(file);
            if (fileName.Contains("header") && fileName.Contains(".big"))
            {
                HeaderFilePath = file;
                break;
            }
        }
        if (HeaderFilePath == "")
        {
            throw new FileNotFoundException("Header file not found");
        }
        
        // Extract the headers.big into a temp folder.
        var extractedHeaderFile = Directory.CreateTempSubdirectory();
        ExtractedHeaderFileFolder = extractedHeaderFile.FullName;
        HeaderBigType = BIG.GetBigType(HeaderFilePath);
        BIG.Extract(HeaderFilePath, ExtractedHeaderFileFolder);
    }

    /// <summary>
    /// Gets the list of packs only if there is a corresponding .dat file to the .hdr
    /// </summary>
    /// <returns>A list of sound pack names</returns>
    /// <exception cref="ArgumentExceptions"></exception>
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
        // Clear Temp folder and archive header.big
    }

}
