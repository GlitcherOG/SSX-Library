using System;
using System.Collections.Generic;
using System.IO;

namespace SSXLibrary.FileHandlers.Audio
{
    /// <summary>
    /// EA "BNKl" sound bank (SSX Tricky PS2 SFX: crowd, board, birds, ambient loops, voices).
    ///
    /// Layout: a "BNKl" header, then a uint32 entry table (one relative offset per sound); each
    /// entry points at a patch-list header (the same 0x80/0x82/0x84/0x85/0xA0/0xFF tag stream
    /// used by SCHl streams) describing one sound. Unlike the streamed music, the sample data is
    /// contiguous raw EA-XA frames (no SCDl blocks) at the offset(s) named by the header.
    ///
    /// This loads the bank and exposes the parsed per-sound headers; <see cref="DecodeSound"/>
    /// turns one sound into interleaved PCM16 via the shared <see cref="EaXaCodec"/>.
    /// </summary>
    public class BnkHandler
    {
        public int Version;
        public int SoundCount;
        public byte[] Data = Array.Empty<byte>();
        public List<BnkSound> Sounds = new List<BnkSound>();

        public class BnkSound
        {
            public int Index;
            public int HeaderOffset;
            public int Channels = 1;
            public int SampleRate;
            public int SampleCount;
            public int Codec = -1;
            // All raw patches (tag -> value), for diagnosis / unknown fields.
            public Dictionary<int, long> Patches = new Dictionary<int, long>();
            // Per-channel data offsets (absolute within the bank), filled from the offset patches.
            public List<int> ChannelOffsets = new List<int>();
        }

        public void Load(string path)
        {
            Data = File.ReadAllBytes(path);
            byte[] d = Data;
            if (d.Length < 0x10 || d[0] != 'B' || d[1] != 'N' || d[2] != 'K')
                throw new InvalidDataException("Not a BNK file (missing 'BNK' magic).");

            Version = d[0x04];
            SoundCount = d[0x06] | (d[0x07] << 8);
            int tableOffset = Version == 2 ? 0x0C : 0x14;

            Sounds.Clear();
            for (int i = 0; i < SoundCount; i++)
            {
                int entryOffset = tableOffset + 4 * i;
                if (entryOffset + 4 > d.Length) break;
                int rel = (int)EaXaCodec.ReadU32(d, entryOffset);
                if (rel == 0) continue;                       // empty slot
                int headerOffset = entryOffset + rel;
                if (headerOffset <= 0 || headerOffset >= d.Length) continue;

                var s = new BnkSound { Index = i, HeaderOffset = headerOffset };
                ParsePatchHeader(d, headerOffset, s);
                Sounds.Add(s);
            }
        }

        // Parse one sound's patch-list header. Each header starts with a 4-byte platform dword
        // ("PT" + platform_u16, same as the SCHl PlatformID field) which we skip, then a tag
        // stream: each tag is a length byte + big-endian value, except 0xFC/0xFD (info markers,
        // no payload) and 0xFE/0xFF (terminators). Verified on garibaldi1.bnk: per-sound headers
        // carry 0x85 (samples), 0x84 (rate), 0x82 (channels), 0x88/0x89 (per-channel data offset);
        // there is no 0xA0 codec tag, so the codec defaults to EA-XA (the PS2 SFX codec).
        static void ParsePatchHeader(byte[] d, int pos, BnkSound s)
        {
            pos += 4; // skip the platform dword ("PT" + platform)

            while (pos < d.Length)
            {
                int tag = d[pos++];
                if (tag == 0xFF || tag == 0xFE) break;   // end of this sound's header
                if (tag == 0xFD || tag == 0xFC) continue; // info markers, no payload

                if (pos >= d.Length) break;
                int size = d[pos++];
                long val = 0;
                for (int k = 0; k < size && pos < d.Length; k++) val = (val << 8) | d[pos++]; // big-endian

                s.Patches[tag] = val;
                switch (tag)
                {
                    case 0x82: s.Channels = (int)val; break;
                    case 0x84: s.SampleRate = (int)val; break;
                    case 0x85: s.SampleCount = (int)val; break;
                    case 0xA0: s.Codec = (int)val; break;
                    case 0x88: s.ChannelOffsets.Insert(0, (int)val); break; // ch0
                    case 0x89: s.ChannelOffsets.Add((int)val); break;       // ch1
                }
            }

            if (s.Codec < 0) s.Codec = 0x05;                 // no 0xA0 tag on PS2 -> native PS-ADPCM (VAG)
            if (s.SampleRate == 0) s.SampleRate = 22050;     // PS2 default (no 0x84 tag)
            if (s.Channels < 1) s.Channels = 1;
        }

        /// <summary>
        /// Decode one sound to interleaved PCM16. Handles the codecs SSX Tricky PS2 banks use:
        /// PS-ADPCM / VAG (0x05, the default when no 0xA0 tag — crowd/ambient), signed 8-bit PCM
        /// (0x09, board/short SFX), and EA-XA ADPCM (0x0A). Returns null if unsupported or a
        /// channel offset is missing.
        /// </summary>
        public short[]? DecodeSound(int index)
        {
            var s = Sounds[index];
            if (s.Codec != 0x05 && s.Codec != 0x09 && s.Codec != 0x0A) return null;
            int nch = s.Channels;
            if (s.ChannelOffsets.Count < nch) return null;    // need a data offset per channel

            var channels = new List<short>[nch];
            for (int c = 0; c < nch; c++)
            {
                channels[c] = new List<short>(s.SampleCount);
                int p = s.ChannelOffsets[c];
                int h1 = 0, h2 = 0;
                if (s.Codec == 0x05)       // PS-ADPCM (VAG): PS2-native, 16-byte frames
                {
                    PsAdpcmCodec.DecodeChannel(Data, ref p, s.SampleCount, channels[c], ref h1, ref h2);
                }
                else if (s.Codec == 0x0A)  // EA-XA: same as SCHl - 15-byte frames (gap 0), v1 rounding.
                {                          // Verified byte-exact vs vgmstream on Wind1.bnk (its "v1" tag = +0x80 rounding).
                    EaXaCodec.DecodeChannel(Data, ref p, s.SampleCount, channels[c], ref h1, ref h2, headerGap: 0, round: true);
                }
                else                       // 0x09: signed 8-bit PCM, one byte per sample -> 16-bit
                {
                    for (int i = 0; i < s.SampleCount && p + i < Data.Length; i++)
                        channels[c].Add((short)((sbyte)Data[p + i] << 8));
                }
            }

            int perCh = channels[0].Count;
            for (int c = 1; c < nch; c++) perCh = Math.Min(perCh, channels[c].Count);
            var outPcm = new short[perCh * nch];
            for (int i = 0; i < perCh; i++)
                for (int c = 0; c < nch; c++)
                    outPcm[i * nch + c] = channels[c][i];
            return outPcm;
        }
    }
}
