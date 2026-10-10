using SSX_Library.Internal.Utilities;

namespace SSXLibrary.FileHandlers.Audio
{
    public class EAAudioHandler
    {
        SCHlHeader schlHeader = new SCHlHeader();
        SCClHeader scclHeader = new SCClHeader();
        List<SCDlHeader> scdlHeaders = new List<SCDlHeader>();

        public void Load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                Load(stream);
            }
        }

        /// <summary>
        /// Load one SCHl stream starting at the stream's CURRENT position (which need not be 0 -
        /// .mus PathFinder files and speech .dat banks are many SCHl streams back to back). Reads
        /// exactly the header + its SCCl/SCDl blocks, leaving the position after the last SCDl.
        /// </summary>
        public void Load(Stream stream)
        {
            {
                //#define EA_BLOCKID_HEADER           0x5343486C /* "SCHl" */
                //#define EA_BLOCKID_COUNT            0x5343436C /* "SCCl" */
                //#define EA_BLOCKID_DATA             0x5343446C /* "SCDl" */
                //#define EA_BLOCKID_END              0x5343456C /* "SCEl" */
                /* Stream is divided into blocks/chunks: SCHl=audio header, SCCl=count of SCDl, SCDl=data xN, SCLl=loop end, SCEl=end.
                   Video uses picture blocks (MVhd/MV0K/etc) and sometimes multiaudio blocks (SHxx/SCxx/SDxx/SExx where xx=language).
                   The number/size is affected by: block rate setting, sample rate, channels, CPU location (SPU/main/DSP/others), etc */

                long chunkStart = stream.Position;
                schlHeader = new SCHlHeader();
                scclHeader = new SCClHeader();
                scdlHeaders.Clear();

                schlHeader.HeaderMagic = StreamUtil.ReadString(stream, 4);
                schlHeader.HeaderSize = StreamUtil.ReadUInt32(stream);
                schlHeader.PlatformID = StreamUtil.ReadUInt32(stream)>>16;

                long headerEnd = chunkStart + schlHeader.HeaderSize;
                while (stream.Position < headerEnd)
                {
                    //More Here but probably unneeded 
                    //https://github.com/vgmstream/vgmstream/blob/master/src/meta/ea_schl.c#L1594
                    int DataType = StreamUtil.ReadUInt8(stream);
                    switch (DataType)
                    {
                        case 0x06:
                            schlHeader.PriorityID = PatchRead(stream);
                            break;
                        case 0x0B:
                            schlHeader.BankChannels = PatchRead(stream);
                            break;
                        case 0xFD:
                            //Info Header Start
                            break;
                        case 0x80:
                            schlHeader.Version = PatchRead(stream);
                            break;
                        case 0x85:
                            schlHeader.SampleCount = PatchRead(stream);
                            break;
                        case 0x82:
                            schlHeader.ChannelCount = PatchRead(stream);
                            break;
                        case 0x84:
                            schlHeader.SampleRate = PatchRead(stream);
                            break;
                        case 0xA0:
                            schlHeader.Codex2Def = PatchRead(stream);
                            break;
                        case 0x8C:
                            schlHeader.Unknown = PatchRead(stream);
                            break;
                        case 0xFF:
                            StreamUtil.AlignBy(stream, 4);
                            break;
                        default:
                            int Temp = PatchRead(stream);
                            //MessageBox.Show($"Error Unkwon Patch Type {DataType} With Data {Temp}");
                            break;
                    }
                }

                if(schlHeader.SampleRate==0)
                {
                    switch (schlHeader.PlatformID)
                    {
                        case 0x05: //PS2
                            schlHeader.SampleRate = 22050;
                            break;
                        default:
                            break;
                    }
                }

                scclHeader.HeaderMagic = StreamUtil.ReadString(stream, 4);
                scclHeader.HeaderSize = StreamUtil.ReadUInt32(stream);
                scclHeader.BlockCount = StreamUtil.ReadUInt32(stream);

                for (int i = 0; i < scclHeader.BlockCount; i++)
                {
                    var NewSCDl = new SCDlHeader();
                    NewSCDl.HeaderMagic = StreamUtil.ReadString(stream, 4);
                    NewSCDl.HeaderSize = StreamUtil.ReadUInt32(stream);
                    NewSCDl.AudioData = StreamUtil.ReadBytes(stream, NewSCDl.HeaderSize - 8);
                    scdlHeaders.Add(NewSCDl);
                }

            }
        }

        // ---- Decoded-stream accessors (valid after Load) ---------------------
        public int SampleRate => schlHeader.SampleRate;
        public int Channels   => schlHeader.ChannelCount > 0 ? schlHeader.ChannelCount : 1;
        public int SampleCount => schlHeader.SampleCount;
        /// <summary>Codec2 patch (0xA0): 0x0A = EA-XA ADPCM (music), 0x04 = MicroTalk 10:1 (speech).</summary>
        public int Codec => schlHeader.Codex2Def;

        /// <summary>
        /// Decode the loaded SCDl blocks into interleaved 16-bit PCM.
        ///
        /// SSX Tricky (PS2) uses EA-XA (codec2 0x0A), split per channel: each SCDl block is
        ///   [block_samples u32][per-channel data offset u32 * channels][channel data...]
        /// and each channel's region is a run of mono EA-XA frames (1 header byte + 14 data
        /// bytes -> 28 samples; header = coef-index in the high nibble, shift in the low).
        /// Predictor history carries continuously across blocks per channel.
        /// </summary>
        /// <returns>Interleaved little-endian PCM16 samples (length = samplesPerChannel * channels).</returns>
        public short[] DecodeAudio()
        {
            if (Codec == 0x04) return DecodeMicroTalk();
            return DecodeEaXa();
        }

        /// <summary>
        /// Decode a MicroTalk (codec2 0x04) stream - SSX Tricky's SPEECH.BIG voice banks. MT SCDl
        /// payload = [block_samples u32][per-channel offset u32 x ch][channel data], and each
        /// channel's block region starts with ONE flag byte before the bitstream (vgmstream:
        /// base + 0x0C + 4*ch + offset + 0x01). Frames are VBR but every block is byte-aligned, so
        /// the bit reader restarts per block while the decoder (LPC history, gains, the once-per-
        /// stream header) carries across - one UtkCodec per channel, block_samples per block.
        /// </summary>
        short[] DecodeMicroTalk()
        {
            int nch = Channels;
            if (nch < 1) nch = 1;

            var channels = new List<short>[nch];
            var utk = new UtkCodec[nch];
            for (int c = 0; c < nch; c++)
            {
                channels[c] = new List<short>(schlHeader.SampleCount);
                utk[c] = new UtkCodec();
                utk[c].Reset();
            }

            foreach (var block in scdlHeaders)
            {
                byte[] d = block.AudioData;
                if (d == null || d.Length < 4 + 4 * nch) continue;
                int blockSamples = (int)EaXaCodec.ReadU32(d, 0);
                int chanBase = 4 + 4 * nch;
                for (int c = 0; c < nch; c++)
                {
                    int start = chanBase + (int)EaXaCodec.ReadU32(d, 4 + 4 * c) + 1;   // +1 flag byte
                    int end = c + 1 < nch ? chanBase + (int)EaXaCodec.ReadU32(d, 4 + 4 * (c + 1)) : d.Length;
                    if (start < 0 || end > d.Length || start > end) continue;
                    var region = new byte[end - start];
                    Array.Copy(d, start, region, 0, region.Length);
                    utk[c].SetBuffer(region);
                    utk[c].Decode(blockSamples, channels[c]);
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

        short[] DecodeEaXa()
        {
            int nch = Channels;
            if (nch < 1) nch = 1;

            // Per-channel running predictor history (carried across blocks).
            int[] hist1 = new int[nch];
            int[] hist2 = new int[nch];

            // Decode each channel into its own buffer, then interleave.
            var channels = new List<short>[nch];
            for (int c = 0; c < nch; c++) channels[c] = new List<short>();

            foreach (var block in scdlHeaders)
            {
                byte[] d = block.AudioData;
                if (d == null || d.Length < 4) continue;

                int blockSamples = (int)EaXaCodec.ReadU32(d, 0);
                // Block body (relative to the SCDl payload, i.e. after magic+size):
                //   [0x00] block_samples (per channel)
                //   [0x04] per-channel data offset u32 * channels   (ch0 = 0, ch1 = ~samples/28*15, ...)
                //   [0x04 + 4*nch] one more u32 (purpose unknown; 0 on silent blocks) -- channel data follows
                // Verified on GARI: with this base the silent intro decodes to 0 and music frames carry
                // valid (low-nibble) coefficient indices.
                int chanBase = 8 + 4 * nch;

                for (int c = 0; c < nch; c++)
                {
                    int chanOffset = (int)EaXaCodec.ReadU32(d, 4 + 4 * c);
                    int p = chanBase + chanOffset;
                    EaXaCodec.DecodeChannel(d, ref p, blockSamples, channels[c], ref hist1[c], ref hist2[c]);
                }
            }

            // Interleave (all channels carry the same sample count).
            int perCh = channels[0].Count;
            for (int c = 1; c < nch; c++) perCh = Math.Min(perCh, channels[c].Count);
            var outPcm = new short[perCh * nch];
            for (int i = 0; i < perCh; i++)
                for (int c = 0; c < nch; c++)
                    outPcm[i * nch + c] = channels[c][i];
            return outPcm;
        }

        public int PatchRead(Stream stream)
        {
            int ByteSize = StreamUtil.ReadInt8(stream);
            byte[] TempValue = StreamUtil.ReadBytes(stream, ByteSize, true);
            byte[] Value = new byte[4];
            for (int i = 0; i < TempValue.Length; i++)
            {
                Value[i] = TempValue[i];
            }
            return BitConverter.ToInt32(Value, 0);

        }

        public struct SCHlHeader
        {
            public string HeaderMagic;
            public int HeaderSize;
            public int PlatformID;

            public int PriorityID;
            public int BankChannels;
            public int Version;
            public int SampleCount;
            public int ChannelCount;
            public int SampleRate;
            public int Codex2Def;
            public int Unknown;
        }

        public struct SCClHeader
        {
            public string HeaderMagic;
            public int HeaderSize;
            public int BlockCount;
        }

        public struct SCDlHeader
        {
            public string HeaderMagic;
            public int HeaderSize;

            public byte[] AudioData;
        }
    }
}

//https://github.com/vgmstream/vgmstream/blob/master/src/meta/ea_schl.c
//https://github.com/vgmstream/vgmstream/blob/master/src/meta/ea_schl_fixed.c
//https://github.com/vgmstream/vgmstream/blob/master/src/meta/ea_schl_streamfile.h
