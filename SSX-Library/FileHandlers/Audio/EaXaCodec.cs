using System.Collections.Generic;

namespace SSXLibrary.FileHandlers.Audio
{
    /// <summary>
    /// EA-XA ADPCM codec (EA codec2 == 0x0A), shared by the SCHl stream decoder
    /// (<see cref="EAAudioHandler"/>) and the BNK sound-bank decoder. The format is a 4-bit
    /// ADPCM: each mono frame is 1 header byte (coefficient index in the high nibble, shift in
    /// the low) followed by 14 data bytes = 28 samples (high nibble first). Predictor history
    /// (hist1/hist2) carries forward; pass the same refs across consecutive frames/blocks.
    /// </summary>
    public static class EaXaCodec
    {
        // Two coefficient arrays packed flat: coef1 = TABLE[index], coef2 = TABLE[index + 4].
        static readonly int[] TABLE =
        {
            0,  240,  460,  392,
            0,    0, -208, -220,
            0,    1,    3,    4,
            7,    8,   10,   11,
            0,   -1,   -3,   -4,
        };

        /// <summary>
        /// Decode <paramref name="sampleCount"/> mono samples starting at <c>d[p]</c>, appending to
        /// <paramref name="outSamples"/> and advancing <paramref name="p"/> and the predictor history.
        /// Data is whole 28-sample frames; decoding stops at sampleCount.
        ///
        /// <paramref name="headerGap"/> is the number of reserved bytes between a frame's header byte
        /// and its 14 data bytes. SCHl streams pack frames to 15 bytes (gap 0); BNK banks pad frames
        /// to 16 bytes with one reserved 0x00 byte after the header (gap 1).
        ///
        /// </summary>
        public static void DecodeChannel(byte[] d, ref int p, int sampleCount, List<short> outSamples, ref int hist1, ref int hist2, int headerGap = 0, bool round = true)
        {
            int rounding = round ? 0x80 : 0;                 // v1 (SCHl) rounds; v2 (BNK) does not
            int produced = 0;
            while (produced < sampleCount)
            {
                int header = d[p++];
                p += headerGap;                              // skip reserved byte(s) before the data
                int index = (header >> 4) & 0x0F;
                int shift = (header & 0x0F) + 8;
                int coef1 = TABLE[index];
                int coef2 = TABLE[index + 4];

                for (int n = 0; n < 28; n++)
                {
                    int b = d[p + (n >> 1)];
                    int nibble = (n & 1) == 0 ? (b >> 4) & 0x0F : b & 0x0F; // high nibble first

                    int sample = (nibble << 28) >> shift;                  // sign-extend + scale
                    sample = (sample + coef1 * hist1 + coef2 * hist2 + rounding) >> 8;
                    sample = Clamp16(sample);

                    hist2 = hist1;
                    hist1 = sample;

                    if (produced < sampleCount) { outSamples.Add((short)sample); produced++; }
                }
                p += 14; // 28 nibbles = 14 data bytes consumed
            }
        }

        public static uint ReadU32(byte[] d, int o) =>
            (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));

        public static short Clamp16(int v) => v < -32768 ? (short)-32768 : v > 32767 ? (short)32767 : (short)v;
    }
}
