using Newtonsoft.Json;
using SSX_Library.FileHandlers.LevelFiles.Tricky.PS2;
using SSXLibrary.JsonFiles.Tricky;

namespace SSX_Library.Tests;

public sealed class ParticleInstanceJsonTests
{
    [Fact]
    public void ParticleModelIndex_UsesCanonicalJsonName_AndReadsLegacyAlias()
    {
        var current = new ParticleInstanceJsonHandler
        {
            Particles =
            {
                new ParticleInstanceJsonHandler.ParticleJson
                {
                    ParticleName = "Fog_Test",
                    ParticleModelIndex = 7,
                },
            },
        };

        string json = JsonConvert.SerializeObject(current);
        Assert.Contains("\"ParticleModelIndex\":7", json);
        Assert.DoesNotContain("\"UnknownInt1\":", json);
        var roundTrip = JsonConvert.DeserializeObject<ParticleInstanceJsonHandler>(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(7, roundTrip.Particles[0].ParticleModelIndex);

        const string legacy = "{\"Particles\":[{\"ParticleName\":\"Fog_Legacy\",\"UnknownInt1\":12}]}";
        var loaded = JsonConvert.DeserializeObject<ParticleInstanceJsonHandler>(legacy);
        Assert.NotNull(loaded);
        Assert.Equal(12, loaded.Particles[0].ParticleModelIndex);
    }

    [Fact]
    public void UnknownInt1_ForwardsToParticleModelIndex()
    {
        var instance = new ParticleInstance();
#pragma warning disable CS0618
        instance.UnknownInt1 = 4;
        Assert.Equal(4, instance.ParticleModelIndex);
        instance.ParticleModelIndex = 9;
        Assert.Equal(9, instance.UnknownInt1);
#pragma warning restore CS0618
    }
}
