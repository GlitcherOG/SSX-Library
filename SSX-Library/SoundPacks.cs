
namespace SSX_Library;

/*
    For now:
    I'll rule out Speaker ID and Event ID editing.  
    
    I'll not add a feature to add individual sounds, You have to extract the whole sound pack,
    replace the one you want to change, and then rebuild again.

    Warning:
    A folder containing.dat files was found right next to a headers.big. This is in ssx Tricky's 
    data/speech/anim
    The headers.big also replicate the fact that the .dat is in a folder by the same name, interesting.

*/

/// <summary>
/// Represents a folder containing sound packs, where each pack holds sounds.
/// </summary>
/// <remarks>
/// A valid folder is considered a SoundPack if it has the following:<br></br>
/// - .dat files <br></br>
/// - A .big archive containing .hdr files <br></br><br></br>
/// Proprietary tools are required to use this class.<br></br>
/// This class's objects must be disposed of after opening - to commit changes to the filesystem.
/// </remarks>
public sealed class SoundPacks : IDisposable
{
    private string FolderPath = "";
    private string AudioToolsPath = "";

    /// <summary>
    /// Open a SoundsPack folder.
    /// </summary>
    /// <param name="audioToolsPath"> The path to the proprietary EA audio tools, 
    /// used for sound extraction/generation.</param>
    public static SoundPacks Open(string folderPath, string audioToolsPath)
    {
        var soundPack = new SoundPacks
        {
            FolderPath = folderPath,
            AudioToolsPath = audioToolsPath
        };

        /*
            Auto-detect which .big file in the directory is the header.big.
            Do techniques like checking file names with "head" while ending in ".big"
            Then check if the big has any .hdr files. 
        */

        return soundPack;
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

    // Make sure the extracted wav file is named after its ID in the .dat file.
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