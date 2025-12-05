// using SSX_Library.Utilities;
// using SSXLibrary.Utilities;
using DiscUtils.Iso9660;
using DiscUtils;
using System.Text;

namespace SSX_Library.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        // string path = "/home/eric/Downloads/Tricky.iso";
        // string dest = "/home/eric/Downloads/Extra";

        // using FileStream isoStream = File.Open(path, FileMode.Open);
        // CDReader cd = new(isoStream, true);
        // ExtractDirectory(cd.Root, dest);

        // CDBuilder builder = new();
        // builder.UseJoliet = true;
        // builder.VolumeIdentifier = "A_SAMPLE_DISK";
        // builder.AddFile("Hello.txt", Encoding.ASCII.GetBytes("Hello World!"));
        // builder.Build(@"\home\eric\Downloads\sample.iso");


    }

    private static void ExtractDirectory(DiscDirectoryInfo directoryInfo, string destinationPath)
    {
        // Create the corresponding directory on the host file system
        string currentDirectoryPath = Path.Combine(destinationPath, directoryInfo.Name.ToLower());
        if (!Directory.Exists(currentDirectoryPath))
        {
            Directory.CreateDirectory(currentDirectoryPath);
        }

        // Extract files in the current directory
        foreach (DiscFileInfo fileInfo in directoryInfo.GetFiles())
        {
            string rawFileName = fileInfo.Name;
            if (rawFileName.EndsWith(";1"))
            {
                rawFileName = rawFileName[..^2];
            }

            string filePath = Path.Combine(currentDirectoryPath, rawFileName.ToLower());
            Console.WriteLine($"Extracting file: {filePath}");

            using Stream fileStream = fileInfo.OpenRead();
            using FileStream outputStream = File.Create(filePath);
            fileStream.CopyTo(outputStream); // Copies the data from the ISO stream to the local file stream
        }

        // Recursively extract subdirectories
        foreach (DiscDirectoryInfo subDirectoryInfo in directoryInfo.GetDirectories())
        {
            ExtractDirectory(subDirectoryInfo, currentDirectoryPath);
        }
    }

    [Fact]
    public void Test2()
    {
//                                26         18 17          9 8            0
//                                |            ||            ||            | 
        // byte[] numbers = [0b0000_0001, 0b0000_0100, 0b0000_1100, 0b0000_0000];
        byte[] numbers = [1, 2, 3, 4, 5, 6, 7];
        byte[] numbers2 = [3];
        // Console.WriteLine(Convert.ToString(output2, 2).PadLeft(8, '0'));
    }
}