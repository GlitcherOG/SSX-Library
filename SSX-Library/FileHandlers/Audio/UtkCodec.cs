// This file is a C# port of the UTK (EA MicroTalk) decoder from vgmstream
// (src/coding/ea_mt_decoder/utkdec.c), https://github.com/vgmstream/vgmstream.
// vgmstream is distributed under the ISC License, whose copyright and permission
// notice are reproduced below as that license requires.
//
//     Copyright (c) 2008-2025 Adam Gashlin, Fastelbja, Ronny Elfert, bnnm,
//     Christopher Snowhill, NicknineTheEagle, bxaimc, Thealexbarney, CyberBotX,
//     EdnessP, et al
//
//     Permission to use, copy, modify, and distribute this software for any
//     purpose with or without fee is hereby granted, provided that the above
//     copyright notice and this permission notice appear in all copies.
//
//     THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
//     WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
//     MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
//     ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
//     WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
//     ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
//     OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
//
// Modifications for SSX-Library are Copyright (C) 2026 swax and, as part of this
// repository, are licensed GPL-3.0. ISC is compatible with the GPL, so the
// combined work may be conveyed under GPL-3.0 with this notice preserved.

using System;
using System.Collections.Generic;

namespace SSXLibrary.FileHandlers.Audio
{
    /// <summary>
    /// EA MicroTalk "MT10:1" speech codec (EA codec2 == 0x04) - the codec of SSX Tricky's
    /// SPEECH.BIG voice banks (announcer / rider chatter / narrator). It's an LPC vocoder:
    /// 432-sample frames carry 12 reflection coefficients plus four 108-sample subframes of
    /// huffman-coded excitation (multipulse or RELP) fed through pitch prediction and a lattice
    /// synthesis filter. Ported from vgmstream's utkdec.c (type UTK_EA, the SCHl-stream variant;
    /// itself matched against EA's original UTALKSTATE code), which is our established decode
    /// oracle for every other SSX codec.
    ///
    /// One instance per channel; the bitstream is continuous across SCDl blocks, so feed each
    /// channel's concatenated bytes once and pull frames until the sample count is met.
    /// </summary>
    public sealed class UtkCodec
    {
        // 'bitmask' table (LSB-first reader).
        static readonly byte[] MaskTable = { 0x01, 0x03, 0x07, 0x0F, 0x1F, 0x3F, 0x7F, 0xFF };

        // 'coeff_table': reflection coefficients; mirrored (t[64-i] = -t[i]).
        static readonly float[] RcTable =
        {
            +0.000000f, -0.996776f, -0.990327f, -0.983879f,
            -0.977431f, -0.970982f, -0.964534f, -0.958085f,
            -0.951637f, -0.930754f, -0.904960f, -0.879167f,
            -0.853373f, -0.827579f, -0.801786f, -0.775992f,
            -0.750198f, -0.724405f, -0.698611f, -0.670635f,
            -0.619048f, -0.567460f, -0.515873f, -0.464286f,
            -0.412698f, -0.361111f, -0.309524f, -0.257937f,
            -0.206349f, -0.154762f, -0.103175f, -0.051587f,
            +0.000000f, +0.051587f, +0.103175f, +0.154762f,
            +0.206349f, +0.257937f, +0.309524f, +0.361111f,
            +0.412698f, +0.464286f, +0.515873f, +0.567460f,
            +0.619048f, +0.670635f, +0.698611f, +0.724405f,
            +0.750198f, +0.775992f, +0.801786f, +0.827579f,
            +0.853373f, +0.879167f, +0.904960f, +0.930754f,
            +0.951637f, +0.958085f, +0.964534f, +0.970982f,
            +0.977431f, +0.983879f, +0.990327f, +0.996776f,
        };

        // 'index_table' huffman codebooks: [model][8-bit peek] -> command index.
        static readonly byte[][] Codebooks =
        {
            new byte[]
            {
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 17,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 21,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 18,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 25,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 17,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 22,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 18,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5,  0,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 17,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 21,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 18,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 26,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 17,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5, 22,
                4,  6,  5,  9,  4,  6,  5, 13,  4,  6,  5, 10,  4,  6,  5, 18,
                4,  6,  5,  9,  4,  6,  5, 14,  4,  6,  5, 10,  4,  6,  5,  2
            },
            new byte[]
            {
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 23,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8, 27,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 24,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8,  1,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 23,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8, 28,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 24,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8,  3,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 23,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8, 27,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 24,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8,  1,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 23,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8, 28,
                4, 11,  7, 15,  4, 12,  8, 19,  4, 11,  7, 16,  4, 12,  8, 24,
                4, 11,  7, 15,  4, 12,  8, 20,  4, 11,  7, 16,  4, 12,  8,  3
            },
        };

        // 'decode_table': command -> (next model, huffman code bit length, pulse value).
        static readonly int[] CmdNextModel =
        {
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        };
        static readonly int[] CmdCodeSize =
        {
            8, 7, 8, 7, 2, 2, 2, 3, 3, 4, 4, 3, 3, 5, 5, 4, 4, 6, 6, 5, 5, 7, 7, 6, 6, 8, 8, 7, 7,
        };
        static readonly float[] CmdPulseValue =
        {
            0, 0, 0, 0, 0, -1, +1, -1, +1, -2, +2, -2, +2, -3, +3, -3, +3, -4, +4, -4, +4, -5, +5, -5, +5, -6, +6, -6, +6,
        };

        // ---- bit reader (LSB-first over one continuous channel buffer) ----
        byte[] _data = Array.Empty<byte>();
        int _pos;
        uint _bitsValue;
        int _bitsCount;

        // ---- decoder state ----
        bool _parsedHeader;
        bool _reducedBandwidth;
        int _multipulseThreshold;
        readonly float[] _fixedGains = new float[64];
        readonly float[] _rcData = new float[12];
        readonly float[] _synthHistory = new float[12];
        readonly float[] _subframes = new float[324 + 432];   // adapt_cb (324) + samples (432), joined

        byte ReadByte() => _pos < _data.Length ? _data[_pos++] : (byte)0;

        void InitBits()
        {
            if (_bitsCount != 0) return;
            _bitsValue = ReadByte();
            _bitsCount = 8;
        }

        byte PeekBits(int count) => (byte)(_bitsValue & MaskTable[count - 1]);

        byte ReadBits(int count)
        {
            byte ret = (byte)(_bitsValue & MaskTable[count - 1]);
            _bitsValue >>= count;
            _bitsCount -= count;
            if (_bitsCount < 8)
            {
                _bitsValue |= (uint)ReadByte() << _bitsCount;
                _bitsCount += 8;
            }
            return ret;
        }

        /// <summary>Full decoder reset - call once per SCHl stream (the 15-bit gain/bandwidth header is parsed once per stream).</summary>
        public void Reset()
        {
            _bitsValue = 0;
            _bitsCount = 0;
            _parsedHeader = false;
            _reducedBandwidth = false;
            _multipulseThreshold = 0;
            Array.Clear(_fixedGains, 0, _fixedGains.Length);
            Array.Clear(_rcData, 0, _rcData.Length);
            Array.Clear(_synthHistory, 0, _synthHistory.Length);
            Array.Clear(_subframes, 0, _subframes.Length);
        }

        /// <summary>
        /// Point the bit reader at one SCDl block's channel region. Frames are VBR and the encoder
        /// byte-aligns each block, so the BIT READER restarts per block while the LPC/codebook state
        /// carries across blocks (vgmstream's flush_ea_mt behaviour).
        /// </summary>
        public void SetBuffer(byte[] data)
        {
            _data = data;
            _pos = 0;
            _bitsValue = 0;
            _bitsCount = 0;
        }

        /// <summary>
        /// Decode <paramref name="sampleCount"/> mono samples from the current buffer (whole
        /// 432-sample frames internally; a final partial frame's tail is dropped, as the next
        /// block restarts byte-aligned).
        /// </summary>
        public void Decode(int sampleCount, List<short> outSamples)
        {
            int produced = 0;
            while (produced < sampleCount)
            {
                DecodeFrame();
                int take = Math.Min(432, sampleCount - produced);
                for (int i = 0; i < take; i++)
                {
                    float s = _subframes[324 + i];
                    s = s >= 0 ? s + 0.5f : s - 0.5f;   // UTK_ROUND
                    if (s > short.MaxValue) s = short.MaxValue;
                    else if (s < short.MinValue) s = short.MinValue;
                    outSamples.Add((short)s);
                }
                produced += take;
            }
        }

        void ParseHeader()
        {
            _reducedBandwidth = ReadBits(1) == 1;
            int baseThre = ReadBits(4);
            int baseGain = ReadBits(4);
            int baseMult = ReadBits(6);

            _multipulseThreshold = 32 - baseThre;
            _fixedGains[0] = 8.0f * (1 + baseGain);
            float multiplier = 1.04f + baseMult * 0.001f;
            for (int i = 1; i < 64; i++)
                _fixedGains[i] = _fixedGains[i - 1] * multiplier;
        }

        void DecodeExcitation(bool useMultipulse, float[] excitation, int offset, int stride)
        {
            int i = 0;
            if (useMultipulse)
            {
                int model = 0;
                while (i < 108)
                {
                    int huffmanCode = PeekBits(8);
                    int cmd = Codebooks[model][huffmanCode];
                    model = CmdNextModel[cmd];
                    ReadBits(CmdCodeSize[cmd]);

                    if (cmd > 3)
                    {
                        excitation[offset + i] = CmdPulseValue[cmd];
                        i += stride;
                    }
                    else if (cmd > 1)
                    {
                        int count = 7 + ReadBits(6);
                        if (i + count * stride > 108)
                            count = (108 - i) / stride;
                        while (count > 0)
                        {
                            excitation[offset + i] = 0.0f;
                            i += stride;
                            count--;
                        }
                    }
                    else
                    {
                        int x = 7;
                        while (ReadBits(1) != 0) x++;
                        if (ReadBits(1) == 0) x = -x;
                        excitation[offset + i] = x;
                        i += stride;
                    }
                }
            }
            else
            {
                while (i < 108)
                {
                    int bits;
                    float val;
                    switch (PeekBits(2))
                    {
                        case 1: val = -2.0f; bits = 2; break;
                        case 3: val = +2.0f; bits = 2; break;
                        default: val = 0.0f; bits = 1; break;   // 00 / 10 -> huffman code '0'
                    }
                    ReadBits(bits);
                    excitation[offset + i] = val;
                    i += stride;
                }
            }
        }

        static void RcToLpc(float[] rcData, float[] lpc)
        {
            var tmp1 = new float[12];
            var tmp2 = new float[12];
            for (int i = 10; i >= 0; i--) tmp2[i + 1] = rcData[i];
            tmp2[0] = 1.0f;

            for (int i = 0; i < 12; i++)
            {
                float x = -(rcData[11] * tmp2[11]);
                for (int j = 10; j >= 0; j--)
                {
                    x -= rcData[j] * tmp2[j];
                    tmp2[j + 1] = x * rcData[j] + tmp2[j];
                }
                tmp2[0] = x;
                tmp1[i] = x;
                for (int j = 0; j < i; j++)
                    x -= tmp1[i - 1 - j] * lpc[j];
                lpc[i] = x;
            }
        }

        void LpSynthesisFilter(int offset, int blocks)
        {
            var lpc = new float[12];
            RcToLpc(_rcData, lpc);

            int ptr = 324 + offset;   // &samples[offset]
            for (int i = 0; i < blocks; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    float x = _subframes[ptr];
                    int k = 0;
                    for (; k < j; k++)
                        x += lpc[k] * _synthHistory[k - j + 12];
                    for (; k < 12; k++)
                        x += lpc[k] * _synthHistory[k - j];
                    _synthHistory[11 - j] = x;
                    _subframes[ptr++] = x;
                }
            }
        }

        static void InterpolateRest(float[] excitation, int offset)
        {
            for (int i = 0; i < 108; i += 2)
            {
                float tmp1 = (excitation[offset + i - 5] + excitation[offset + i + 5]) * 0.01803268f;
                float tmp2 = (excitation[offset + i - 3] + excitation[offset + i + 3]) * 0.11459156f;
                float tmp3 = (excitation[offset + i - 1] + excitation[offset + i + 1]) * 0.59738597f;
                excitation[offset + i] = tmp1 - tmp2 + tmp3;
            }
        }

        void DecodeFrame()
        {
            bool useMultipulse = false;
            var excitation = new float[5 + 108 + 5];
            var rcDelta = new float[12];

            InitBits();
            if (!_parsedHeader)
            {
                ParseHeader();
                _parsedHeader = true;
            }

            for (int i = 0; i < 12; i++)
            {
                int idx;
                if (i == 0)
                {
                    idx = ReadBits(6);
                    if (idx < _multipulseThreshold) useMultipulse = true;
                }
                else if (i < 4) idx = ReadBits(6);
                else idx = 16 + ReadBits(5);

                rcDelta[i] = (RcTable[idx] - _rcData[i]) * 0.25f;
            }

            for (int i = 0; i < 4; i++)
            {
                int pitchLag = ReadBits(8);
                int pitchValue = ReadBits(4);
                int gainIndex = ReadBits(6);

                float pitchGain = pitchValue / 15.0f;
                float fixedGain = _fixedGains[gainIndex];

                if (!_reducedBandwidth)
                {
                    DecodeExcitation(useMultipulse, excitation, 5, 1);
                }
                else
                {
                    int align = ReadBits(1);
                    int zeroFlag = ReadBits(1);

                    DecodeExcitation(useMultipulse, excitation, 5 + align, 2);

                    if (zeroFlag != 0)
                    {
                        for (int j = 0; j < 54; j++)
                            excitation[5 + (1 - align) + 2 * j] = 0.0f;
                    }
                    else
                    {
                        for (int j = 0; j < 5; j++) { excitation[j] = 0.0f; excitation[5 + 108 + j] = 0.0f; }
                        InterpolateRest(excitation, 5 + (1 - align));
                        fixedGain *= 0.5f;
                    }
                }

                for (int j = 0; j < 108; j++)
                {
                    int idx = 108 * i + 216 - pitchLag + j;   // index into adapt_cb (may run into samples)
                    if (idx < 0) idx = 0;
                    float tmp1 = fixedGain * excitation[5 + j];
                    float tmp2 = pitchGain * _subframes[idx];
                    _subframes[324 + 108 * i + j] = tmp1 + tmp2;
                }
            }

            for (int i = 0; i < 324; i++)
                _subframes[i] = _subframes[324 + 108 + i];   // adapt_cb <- samples[108..432]

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 12; j++)
                    _rcData[j] += rcDelta[j];
                int blocks = i < 3 ? 1 : 33;
                LpSynthesisFilter(12 * i, blocks);
            }
        }
    }
}
