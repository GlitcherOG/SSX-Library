using SSX_Library.Internal.BIG;

namespace SSX_Library;

/*
    More Info: https://ssx.computernewb.com/wiki/Formats/Common:BIG
*/

/// <summary>
/// Big files
/// </summary>
public static class BIG
{
    /// <summary>
    /// Gets the type of the big file by reading the magic signature
    /// </summary>
    public static BigType GetBigType(string bigPath)
    {
        using var bigStream = File.OpenRead(bigPath);
        if (COFB.IsStreamCOFB(bigStream))
        {
            return BigType.C0FB;
        }
        if (NewBig.IsStreamNewBig(bigStream))
        {
            return BigType.NewBig;
        }
        if (BIGF4.IsStreamBIGF(bigStream))
        {
            return BigType.BIGF;
        }
        if (BIGF4.IsStreamBIG4(bigStream))
        {
            return BigType.BIG4;
        } 
        throw new InvalidDataException("Big signature not found");
    }

    /// <summary>
    /// Get a list of info for the member files inside a big file.
    /// </summary>
    public static List<MemberFileInfo> GetMembersInfo(string bigPath)
    {
        using var bigStream = File.OpenRead(bigPath);
        if (COFB.IsStreamCOFB(bigStream))
        {
            return COFB.GetMembersInfo(bigPath);
        }
        if (NewBig.IsStreamNewBig(bigStream))
        {
            return NewBig.GetMembersInfo(bigPath);
        }
        if (BIGF4.IsStreamBIGF(bigStream) || BIGF4.IsStreamBIG4(bigStream))
        {
            return BIGF4.GetMembersInfo(bigPath);
        } 
        throw new InvalidDataException("Big signature not found");
    }

    /// <summary>
    /// Extracts a big file into a folder.
    /// </summary>
    public static void Extract(string bigPath, string extractionFolder)
    {
        using var bigStream = File.OpenRead(bigPath);
        if (COFB.IsStreamCOFB(bigStream))
        {
            COFB.Extract(bigPath, extractionFolder);
        }
        if (NewBig.IsStreamNewBig(bigStream))
        {
            NewBig.Extract(bigPath, extractionFolder);
        }
        if (BIGF4.IsStreamBIGF(bigStream) || BIGF4.IsStreamBIG4(bigStream))
        {
            BIGF4.Extract(bigPath, extractionFolder);
        } 
        throw new InvalidDataException("Big signature not found");
    }

    /// <summary>
    /// Create a big file from a folder.
    /// </summary>
    /// <param name="useCompression">Should compress all files with refpack/ChunkZip.</param>
    /// <param name="useBackslashes">Should member files store their paths using backslash.</param>
    public static void Create(BigType bigType, string inputFolderPath, string bigOutputPath, bool useCompression, bool useBackslashes = false)
    {
        switch (bigType)
        {
            case BigType.C0FB:
                COFB.Create(inputFolderPath, bigOutputPath, useCompression, useBackslashes);
                break;
            case BigType.BIGF:
                BIGF4.Create(BigType.BIGF, inputFolderPath, bigOutputPath, useCompression, useBackslashes);
                break;
            case BigType.BIG4:
                BIGF4.Create(BigType.BIG4, inputFolderPath, bigOutputPath, useCompression, useBackslashes);
                break;
            case BigType.NewBig:
                NewBig.Create(inputFolderPath, bigOutputPath, useCompression);
                break;
            default:
                break;
        }
    }
}