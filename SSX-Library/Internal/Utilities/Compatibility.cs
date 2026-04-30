
namespace SSX_Library.Internal.Utilities;

internal static class Compatibility
{
    public enum Platform
    {
        Windows,
        Linux,
    }

    public static Platform GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return Platform.Windows;
        } 
        else if (OperatingSystem.IsLinux())
        {
            return Platform.Linux;
        }
        else
        {
            throw new SystemException("This OS is not supported");
        }
    }
}
