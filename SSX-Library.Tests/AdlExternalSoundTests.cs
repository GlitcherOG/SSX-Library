using SSX_Library.FileHandlers.LevelFiles.Tricky;

namespace SSX_Library.Tests;

public sealed class AdlExternalSoundTests
{
    [Fact]
    public void SaveLoad_PreservesAllVariableSizedExternalSoundTypes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"swx-adl-{Guid.NewGuid():N}.adl");
        try
        {
            var adl = new ADLHandler();
            adl.HashSounds.Add(new ADLHandler.HashSound
            {
                Hash = 1234,
                Sound = new ADLHandler.SoundData
                {
                    CollisonSound = 25,
                    ExternalSounds = new List<ADLHandler.ExternalSound>
                    {
                        Sound(0, 97, 10),
                        Sound(1, 68, 20),
                        Sound(2, 134, 30),
                        Sound(3, 160, 40),
                    },
                },
            });

            adl.Save(path);
            Assert.Equal(177, new FileInfo(path).Length); // header+row+sound data+(0x1c+0x30+0x30+0x18)+sentinel

            var loaded = new ADLHandler();
            loaded.Load(path);
            var sounds = loaded.HashSounds.Single().Sound.ExternalSounds;
            Assert.Equal(new[] { 0, 1, 2, 3 }, sounds.Select(x => x.U0));
            Assert.Equal(21, sounds[1].U2);
            Assert.Equal(30, sounds[1].U11);
            Assert.Equal(31, sounds[2].U2);
            Assert.Equal(40, sounds[2].U11);
            Assert.Equal(41, sounds[3].U2);
            Assert.Equal(44, sounds[3].U5);
            Assert.Equal(0, sounds[3].U6);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    static ADLHandler.ExternalSound Sound(int type, int soundIndex, float seed) => new()
    {
        U0 = type, SoundIndex = soundIndex,
        U2 = seed + 1, U3 = seed + 2, U4 = seed + 3, U5 = seed + 4, U6 = seed + 5,
        U7 = seed + 6, U8 = seed + 7, U9 = seed + 8, U10 = seed + 9, U11 = seed + 10,
    };
}
