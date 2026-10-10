using SSX_Library.FileHandlers.LevelFiles.Tricky.PS2;

namespace SSX_Library.Tests;

/// <summary>
/// Effect nodes are variable length: the size field counts the whole node, and the engine's chain
/// walker advances by it. These cover the two opcodes that rely on that -- main type 12, whose
/// payload is an inline UTF-16LE string, and the unknown-opcode fallback that keeps any main type
/// this reader has no branch for.
///
/// The fallback is the one that matters most. It used to <c>return null</c>, and the chain loop
/// treats null as "stop reading this chain", so a single unrecognised node silently deleted itself
/// AND every node after it -- with both diagnostics commented out.
/// </summary>
public sealed class SsfHudTextRoundTripTests
{
    const int UnknownMainType = 19;   // real: dispatcher entry 19 is wired to the inert default
    const int HeaderBytes = 8;        // MainType + ByteSize

    static SSFHandler.Effect Wait(float seconds) =>
        new() { MainType = 4, WaitTime = seconds };

    static SSFHandler.Effect HudText(string text, float r = 1f, float g = 1f, float b = 1f) =>
        new() { MainType = 12, HudText = text, HudRed = r, HudGreen = g, HudBlue = b };

    static SSFHandler.Effect Unknown(byte[] payload) =>
        new() { MainType = UnknownMainType, UnknownPayload = payload };

    /// <summary>Write a chain, read it back, and return what survived.</summary>
    static List<SSFHandler.Effect> RoundTrip(params SSFHandler.Effect[] chain)
    {
        var handler = new SSFHandler();
        using var stream = new MemoryStream();

        foreach (var effect in chain)
        {
            handler.SaveEffectData(stream, effect);
        }

        stream.Position = 0;
        var read = new List<SSFHandler.Effect>();
        while (stream.Position < stream.Length)
        {
            var effect = handler.LoadEffectsData(stream);
            if (effect == null)
            {
                break;   // exactly what the real chain loop does
            }
            read.Add(effect.Value);
        }
        return read;
    }

    [Fact]
    public void HudText_SurvivesTheRoundTrip()
    {
        var read = RoundTrip(HudText("flag-set-plain"));

        Assert.Single(read);
        Assert.Equal(12, read[0].MainType);
        Assert.Equal("flag-set-plain", read[0].HudText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("ride-over-button")]
    public void HudText_NodeStaysWordAligned(string text)
    {
        // Not cosmetic: the walker adds the size field to a byte offset and the next node's main
        // type is read with a word load, which faults on the EE when it lands unaligned.
        var read = RoundTrip(HudText(text));

        Assert.Equal(0, read[0].ByteSize % 4);
        Assert.True(read[0].ByteSize >= HeaderBytes);
        Assert.Equal(text, read[0].HudText);
    }

    [Fact]
    public void TheColourSurvivesTheRoundTrip()
    {
        var read = RoundTrip(HudText("boost-directional", 0.25f, 0.5f, 1f));

        Assert.Equal(0.25f, read[0].HudRed);
        Assert.Equal(0.5f, read[0].HudGreen);
        Assert.Equal(1f, read[0].HudBlue);
        Assert.Equal("boost-directional", read[0].HudText);
    }

    [Fact]
    public void TheColourSitsAheadOfTheTextSoOnePointerReachesBoth()
    {
        // The engine hands the shim ONE word -- the text -- and the colour is found at a fixed
        // negative offset from it. Writing it after the string would put it at an offset that
        // depends on the message's length, which nothing on the engine side knows.
        var handler = new SSFHandler();
        using var stream = new MemoryStream();
        handler.SaveEffectData(stream, HudText("ab", 1f, 0f, 0f));
        byte[] node = stream.ToArray();

        Assert.Equal(1f, BitConverter.ToSingle(node, 8));    // R, straight after MainType + ByteSize
        Assert.Equal(0f, BitConverter.ToSingle(node, 12));   // G
        Assert.Equal(0f, BitConverter.ToSingle(node, 16));   // B
        Assert.Equal("ab", System.Text.Encoding.Unicode.GetString(node, 20, 4));
    }

    [Fact]
    public void ANodeTooShortToHoldAColourIsRefused()
    {
        using var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write(12);
        writer.Write(16);        // 8 header + 8 payload: not enough for three channels
        writer.Write(0L);
        writer.Flush();
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => new SSFHandler().LoadEffectsData(stream));
    }

    [Fact]
    public void UnknownOpcode_KeepsItsBytes()
    {
        var payload = new byte[] { 1, 2, 3, 4, 0xDE, 0xAD, 0xBE, 0xEF };
        var read = RoundTrip(Unknown(payload));

        Assert.Single(read);
        Assert.Equal(UnknownMainType, read[0].MainType);
        Assert.Equal(payload, read[0].UnknownPayload);
    }

    [Fact]
    public void UnknownOpcode_DoesNotSwallowTheRestOfItsChain()
    {
        // The regression this whole change exists for. Before the fix this returned one node.
        var read = RoundTrip(
            Wait(3.0f),
            Unknown(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }),
            HudText("after-the-unknown"),
            Wait(1.5f));

        Assert.Equal(4, read.Count);
        Assert.Equal(4, read[0].MainType);
        Assert.Equal(3.0f, read[0].WaitTime);
        Assert.Equal(UnknownMainType, read[1].MainType);
        Assert.Equal(12, read[2].MainType);
        Assert.Equal("after-the-unknown", read[2].HudText);
        Assert.Equal(1.5f, read[3].WaitTime);
    }

    [Fact]
    public void HudText_DoesNotDisturbTheNodesAroundIt()
    {
        var read = RoundTrip(Wait(3.0f), HudText("cracked-tough"), Wait(0.25f));

        Assert.Equal(3, read.Count);
        Assert.Equal(3.0f, read[0].WaitTime);
        Assert.Equal("cracked-tough", read[1].HudText);
        Assert.Equal(0.25f, read[2].WaitTime);
    }

    [Fact]
    public void ByteSize_CountsTheWholeNodeIncludingItsHeader()
    {
        // What the engine's walker relies on, asserted directly rather than inferred from the fact
        // that the nodes around it happen to survive.
        var handler = new SSFHandler();
        using var stream = new MemoryStream();
        handler.SaveEffectData(stream, HudText("abc"));

        stream.Position = 0;
        var effect = handler.LoadEffectsData(stream)!.Value;

        Assert.Equal(stream.Length, effect.ByteSize);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void ACorruptSizeIsRefusedRatherThanSeekingBackwards()
    {
        // A size below the header would move the chain loop backwards and hang it. Reading a
        // hand-built node is the only way to reach this, since the writer cannot emit one.
        using var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write(UnknownMainType);
        writer.Write(4);              // impossible: smaller than the header it counts
        writer.Flush();
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => new SSFHandler().LoadEffectsData(stream));
    }
}
