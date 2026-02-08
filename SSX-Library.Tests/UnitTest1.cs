// using SSXLibrary;
using SSX_Library.Internal;
using SSXLibrary.FileHandlers;

namespace SSX_Library.Tests;

public class UnitTest1
{
    // [Fact]
    // public void TestRefpackDecompressionParity()
    // {
    //     var samplePath = "/home/eric/Downloads/Refpack_sample.bin";
    //     if (!File.Exists(samplePath))
    //     {
    //         throw new FileNotFoundException($"Sample file not found at {samplePath}. Please ensure the sample exists for testing.");
    //     }

    //     var sample = File.ReadAllBytes(samplePath);

    //     // Old implementation
    //     var outputOld = RefpackHandler.Decompress(sample);

    //     // New implementation
    //     var outputNew = Refpack.Decompress(sample);

    //     // Compare results
    //     Assert.NotNull(outputOld);
    //     Assert.NotNull(outputNew);
    //     Assert.Equal(outputOld.Length, outputNew.Length);
    //     Assert.Equal(outputOld, outputNew);

    //     // Optional: save for manual inspection if needed
    //     File.WriteAllBytes("/home/eric/Downloads/Decompressed_Refpack_sample_new.bin", outputNew);
    // }

    [Fact]
    public void TestDecompressedFilesParity()
    {
        // var oldFilePath = "/home/eric/Downloads/Decompressed_Refpack_sample.bin";
        // var newFilePath = "/home/eric/Downloads/Decompressed_Refpack_sample_new.bin";

        // if (!File.Exists(oldFilePath) || !File.Exists(newFilePath))
        // {
        //     throw new FileNotFoundException("One or both decompressed files are missing. Run TestRefpackDecompressionParity first and ensure the old file exists.");
        // }

        // var oldBytes = File.ReadAllBytes(oldFilePath);
        // var newBytes = File.ReadAllBytes(newFilePath);

        // Assert.Equal(oldBytes.Length, newBytes.Length);
        // Assert.Equal(oldBytes, newBytes);
    }
}