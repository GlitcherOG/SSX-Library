using SSX_Library.FileHandlers.LevelFiles.Tricky.PS2;

namespace SSX_Library.Tests;

public sealed class SsfEmitterRoundTripTests
{
    [Fact]
    public void Type2Sub0_U9_RoundTripsAsFloat()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ssf-emitter-{Guid.NewGuid():N}.ssf");
        try
        {
            var emitter = new SSFHandler.Type2Sub0
            {
                U0 = 1,
                U1 = 1,
                U2 = 1.0f,
                U4 = 1.0f,
                U9 = 123.25f,
                U10 = -45.5f,
                U11 = 67.75f,
                U33 = 1.0f,
                U34 = 1.0f,
                U35 = 1.0f,
                U36 = 1.0f,
            };
            var effect = new SSFHandler.Effect
            {
                MainType = 2,
                type2 = new SSFHandler.Type2 { SubType = 0, type2Sub0 = emitter },
            };
            var handler = new SSFHandler();
            handler.EffectSlots.Add(new SSFHandler.EffectSlot
            {
                Slot1 = 0,
                Slot2 = -1,
                Slot3 = -1,
                Slot4 = -1,
                Slot5 = -1,
                Slot6 = -1,
                Slot7 = -1,
            });
            handler.EffectHeaders.Add(new SSFHandler.EffectHeaderStruct
            {
                Effects = new List<SSFHandler.Effect> { effect },
            });
            handler.ObjectProperties.Add(new SSFHandler.ObjectPropertiesStruct
            {
                CollsionMode = 0,
                CollisonModelIndex = -1,
                EffectSlotIndex = 0,
                PhysicsIndex = -1,
            });
            handler.InstanceState.Add(0);

            handler.Save(path);

            var loaded = new SSFHandler();
            loaded.Load(path);
            var actual = loaded.EffectHeaders[0].Effects[0].type2!.Value.type2Sub0!.Value;
            Assert.Equal(123.25f, actual.U9);
            Assert.Equal(-45.5f, actual.U10);
            Assert.Equal(67.75f, actual.U11);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
