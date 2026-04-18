using SSX_Library.Internal.Textures;

namespace SSX_Library;

/// <summary>
/// Mipmap texture files for the SSX3 Gizmondo version.
/// </summary>
public static class XTF
{
    /// <summary>
    /// Convert an XFT texture to a PNG image for each mipmap level.
    /// </summary>
    /// <param name="intputPath">Path to the XFT texture</param>
    /// <param name="outputFolder"></param>
    public static void ToPngs(string intputPath, string outputFolder)
    {
        Gizmondo.XtfToPngs(intputPath, outputFolder);
    }
}