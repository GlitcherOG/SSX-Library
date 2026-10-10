using System.Collections.Generic;

namespace SSXLibrary.FileHandlers.Audio
{
    /// <summary>
    /// Sony PlayStation 4-bit ADPCM ("VAG"), the PS2's native SPU audio format. SSX Tricky's BNK
    /// sound effects that carry no explicit EA codec tag are stored in this format (verified against
    /// vgmstream, which reports "PlayStation 4-bit ADPCM" for them).
    ///
    /// Each frame is 16 bytes: byte 0 = predictor index (high nibble) + shift (low nibble),
    /// byte 1 = loop/end flag (ignored for decode), bytes 2..15 = 14 data bytes = 28 samples, with
    /// the LOW nibble decoded first. The predictor history (hist1/hist2) carries across frames.
    /// </summary>
    public static class PsAdpcmCodec
    {
        // Standard VAG predictor coefficients (scaled by 64; applied as (f0*h1 + f1*h2) >> 6).
        static readonly int[] F0 = { 0, 60, 115, 98, 122 };
        static readonly int[] F1 = { 0, 0, -52, -55, -60 };

        /// <summary>
        /// Decode <paramref name="sampleCount"/> mono samples of PS-ADPCM starting at <c>d[p]</c>,
        /// appending to <paramref name="outSamples"/> and advancing <paramref name="p"/> and history.
        /// </summary>
        public static void DecodeChannel(byte[] d, ref int p, int sampleCount, List<short> outSamples, ref int hist1, ref int hist2)
        {
            int produced = 0;
            while (produced < sampleCount && p + 16 <= d.Length)
            {
                int head = d[p];
                int predictor = (head >> 4) & 0x0F;
                int shift = head & 0x0F;
                if (predictor > 4) predictor = 0;            // guard against out-of-range index
                int f0 = F0[predictor], f1 = F1[predictor];
                // d[p+1] is the loop/end flag byte; not needed to decode samples.

                for (int n = 0; n < 28; n++)
                {
                    int b = d[p + 2 + (n >> 1)];
                    int nibble = (n & 1) == 0 ? (b & 0x0F) : (b >> 4) & 0x0F; // low nibble first

                    int s = (short)(nibble << 12) >> shift;  // sign-extend 4-bit, then scale
                    int sample = s + ((f0 * hist1 + f1 * hist2) >> 6);

                    // The predictor feeds back the UN-clamped value (matches the SPU's extra internal
                    // precision before the output stage); only the output is clamped. Verified
                    // byte-exact against vgmstream.
                    hist2 = hist1;
                    hist1 = sample;

                    if (produced < sampleCount) { outSamples.Add(EaXaCodec.Clamp16(sample)); produced++; }
                }
                p += 16;
            }
        }
    }
}
