using SSX_Library;
// using SSXLibrary.Utilities;
using DiscUtils;

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
        // string path = "/home/eric/Downloads/letter.loc";
        // string path = "/home/eric/Downloads/cramer.loc";
        // string path = "/home/eric/Downloads/FEAMER_Ontour.LOC";
        string path = "/home/eric/Downloads/american.loc";

        LOC loc = new();
        loc.Load(path);

        // Console.WriteLine(loc.filePath);



    }
}