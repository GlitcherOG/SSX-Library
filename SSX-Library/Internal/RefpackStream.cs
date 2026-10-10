namespace SSX_Library.Internal;

/*
    More Info on the bitstream structure: http://wiki.niotso.org/RefPack

    Bounded Refpack compressor. Compresses as much of an input stream as fits
    into a fixed output size in a single pass, instead of compressing a slice
    and checking the size afterwards.
*/

internal enum RefpackStreamResult
{
    /// <summary>All remaining input was compressed.</summary>
    Complete,

    /// <summary>
    /// The output limit was reached before the end of the input. The input stream
    /// is left at the first byte that was not compressed.
    /// </summary>
    Partial,

    /// <summary>Nothing could be compressed within the limit. Nothing was written.</summary>
    Failed,
}

internal static class RefpackStream
{
    private const int HeaderLength = 5;            // flags, 0xFB, 24-bit decompressed size
    private const int MaxDecompressedSize = 0xFFFFFF;
    private const int MinMatch = 3;
    private const int MaxMatch = 1028;
    private const int MaxDistance = 131072;
    private const int WindowMask = MaxDistance - 1;
    private const int MaxLiteralBlock = 112;
    private const int HashBits = 16;
    private const int MaxChainDepth = 128;         // higher = better compression, slower
    private const int LazyMatchLimit = 64;         // skip the lazy check for matches this long

    /// <summary>
    /// Compresses data from the input stream's current position into the output stream,
    /// writing the Refpack header and as much data as fits in maxOutputLength bytes.
    /// On Partial, input.Position is left at the first byte not compressed, so the
    /// caller can call again to continue into the next block.
    /// </summary>
    /// <param name="input">Seekable stream to read from.</param>
    /// <param name="output">Stream to write the compressed data to.</param>
    /// <param name="maxOutputLength">Maximum bytes to write, including the header.</param>
    /// <param name="maxInputLength">Maximum bytes to consume (the decompressed size of this stream).</param>
    public static RefpackStreamResult Compress(Stream input, Stream output, int maxOutputLength, int maxInputLength = MaxDecompressedSize)
    {
        if (!input.CanSeek)
        {
            throw new ArgumentException("Input stream must be seekable.", nameof(input));
        }

        long startPosition = input.Position;
        long remaining = input.Length - startPosition;
        int limit = (int)Math.Min(remaining, Math.Min(maxInputLength, MaxDecompressedSize));

        // Header plus at least a stop command with one literal
        if (maxOutputLength < HeaderLength + 2)
        {
            return RefpackStreamResult.Failed;
        }

        var compressor = new Compressor(input, limit, maxOutputLength);
        int consumed = compressor.Run();

        if (consumed == 0 && remaining > 0)
        {
            input.Position = startPosition;
            return RefpackStreamResult.Failed;
        }

        output.Write(compressor.Output, 0, compressor.OutputLength);
        input.Position = startPosition + consumed;

        return consumed == remaining ? RefpackStreamResult.Complete : RefpackStreamResult.Partial;
    }

    private sealed class Compressor
    {
        private readonly Stream input;
        private readonly int limit;
        private readonly byte[] output;
        private readonly int maxOutput;
        private int outPos = HeaderLength;

        private byte[] data;
        private int available;

        private readonly int[] head = new int[1 << HashBits];
        private readonly int[] prev = new int[MaxDistance];

        // Literals waiting to be written: data[literalStart .. literalStart + literalCount)
        private int literalStart;
        private int literalCount;

        public byte[] Output => output;
        public int OutputLength => outPos;

        public Compressor(Stream input, int limit, int maxOutput)
        {
            this.input = input;
            this.limit = limit;
            this.maxOutput = maxOutput;
            output = new byte[maxOutput];

            // Most blocks need a few times the output size; grows on demand for very compressible data
            data = new byte[Math.Min(limit, Math.Max(maxOutput * 4, 4096))];
            Array.Fill(head, -1);
        }

        /// <summary>Compresses until the input or output runs out. Returns bytes consumed.</summary>
        public int Run()
        {
            int i = 0;

            while (true)
            {
                EnsureAvailable(i + MaxMatch + 1);
                if (i >= available)
                {
                    break;
                }

                FindMatch(i, out int length, out int distance);
                Insert(i);

                // Lazy matching: if the next position has a longer match, take this byte as a literal
                if (length >= MinMatch && length < LazyMatchLimit && i + 1 < available)
                {
                    FindMatch(i + 1, out int nextLength, out _);
                    if (nextLength > length)
                    {
                        length = 0;
                    }
                }

                if (length < MinMatch)
                {
                    literalCount++;
                    i++;

                    if (literalCount == MaxLiteralBlock && !TryWriteLiteralBlock(MaxLiteralBlock))
                    {
                        break;
                    }
                    continue;
                }

                if (!TryWriteMatch(length, distance))
                {
                    break;
                }

                for (int k = 1; k < length; k++)
                {
                    Insert(i + k);
                }
                i += length;
                literalStart = i;
            }

            Finish();
            WriteHeader(literalStart);
            return literalStart;
        }

        /// <summary>Reads more input until at least `needed` bytes are buffered, or the limit is hit.</summary>
        private void EnsureAvailable(int needed)
        {
            needed = Math.Min(needed, limit);
            if (available >= needed)
            {
                return;
            }

            if (needed > data.Length)
            {
                Array.Resize(ref data, (int)Math.Min(limit, Math.Max((long)data.Length * 2, needed)));
            }

            while (available < data.Length)
            {
                int read = input.Read(data, available, data.Length - available);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Input stream ended before its reported length.");
                }
                available += read;
            }
        }

        private int Hash(int pos)
        {
            uint value = (uint)(data[pos] << 16 | data[pos + 1] << 8 | data[pos + 2]);
            return (int)((value * 2654435761u) >> (32 - HashBits));
        }

        private void Insert(int pos)
        {
            if (pos + MinMatch > available)
            {
                return;
            }

            int hash = Hash(pos);
            prev[pos & WindowMask] = head[hash];
            head[hash] = pos;
        }

        private void FindMatch(int pos, out int bestLength, out int bestDistance)
        {
            bestLength = 0;
            bestDistance = 0;

            int maxLength = Math.Min(MaxMatch, available - pos);
            if (maxLength < MinMatch)
            {
                return;
            }

            int candidate = head[Hash(pos)];
            int depth = MaxChainDepth;
            int compareAt = MinMatch - 1;

            while (candidate >= 0 && depth-- > 0)
            {
                int distance = pos - candidate;
                if (distance > MaxDistance)
                {
                    break;
                }

                // Quick reject: must beat the current best at its last byte
                if (data[candidate + compareAt] == data[pos + compareAt])
                {
                    int length = 0;
                    while (length < maxLength && data[candidate + length] == data[pos + length])
                    {
                        length++;
                    }

                    if (length > bestLength && IsEncodable(length, distance))
                    {
                        bestLength = length;
                        bestDistance = distance;
                        if (length == maxLength)
                        {
                            break;
                        }
                        compareAt = length;
                    }
                }

                int next = prev[candidate & WindowMask];
                if (next >= candidate)
                {
                    break;
                }
                candidate = next;
            }
        }

        private static bool IsEncodable(int length, int distance)
        {
            return (length >= 3 && distance <= 1024)
                || (length >= 4 && distance <= 16384)
                || (length >= 5 && distance <= MaxDistance);
        }

        private static int MatchCommandLength(int length, int distance)
        {
            if (length <= 10 && distance <= 1024) return 2;
            if (length <= 67 && distance <= 16384) return 3;
            return 4;
        }

        /// <summary>
        /// Writes the pending literals and a match command, if they fit while still
        /// leaving room for the stop command.
        /// </summary>
        private bool TryWriteMatch(int length, int distance)
        {
            int trailing = literalCount & 3;
            int blockLiterals = literalCount - trailing;
            int blockCount = (blockLiterals + MaxLiteralBlock - 1) / MaxLiteralBlock;
            int commandLength = MatchCommandLength(length, distance);

            int cost = blockLiterals + blockCount + commandLength + trailing;
            if (outPos + cost + 1 > maxOutput)
            {
                return false;
            }

            while (literalCount > 3)
            {
                WriteLiteralBlock(Math.Min(MaxLiteralBlock, literalCount & ~3));
            }

            int offset = distance - 1;
            int p = literalCount;

            if (commandLength == 2)
            {
                output[outPos++] = (byte)(((offset >> 3) & 0x60) | ((length - 3) << 2) | p);
                output[outPos++] = (byte)offset;
            }
            else if (commandLength == 3)
            {
                output[outPos++] = (byte)(0x80 | (length - 4));
                output[outPos++] = (byte)((p << 6) | (offset >> 8));
                output[outPos++] = (byte)offset;
            }
            else
            {
                output[outPos++] = (byte)(0xC0 | ((offset >> 12) & 0x10) | (((length - 5) >> 6) & 0x0C) | p);
                output[outPos++] = (byte)(offset >> 8);
                output[outPos++] = (byte)offset;
                output[outPos++] = (byte)(length - 5);
            }

            WritePendingLiterals(p);
            return true;
        }

        private bool TryWriteLiteralBlock(int count)
        {
            if (outPos + 1 + count + 1 > maxOutput)
            {
                return false;
            }

            WriteLiteralBlock(count);
            return true;
        }

        private void WriteLiteralBlock(int count)
        {
            output[outPos++] = (byte)(0xE0 | ((count >> 2) - 1));
            WritePendingLiterals(count);
        }

        private void WritePendingLiterals(int count)
        {
            Array.Copy(data, literalStart, output, outPos, count);
            outPos += count;
            literalStart += count;
            literalCount -= count;
        }

        /// <summary>
        /// Writes as many pending literals as fit, then the stop command (which carries up to 3).
        /// Literals that don't fit are left unconsumed.
        /// </summary>
        private void Finish()
        {
            while (literalCount > 3)
            {
                int fit = (maxOutput - outPos - 2) & ~3; // room after the block command and stop command
                int count = Math.Min(Math.Min(MaxLiteralBlock, literalCount & ~3), fit);
                if (count < 4)
                {
                    break;
                }
                WriteLiteralBlock(count);
            }

            int trailing = Math.Min(Math.Min(literalCount, 3), maxOutput - outPos - 1);
            output[outPos++] = (byte)(0xFC | trailing);
            WritePendingLiterals(trailing);
            literalCount = 0;
        }

        private void WriteHeader(int decompressedSize)
        {
            output[0] = 0x10;
            output[1] = 0xFB;
            output[2] = (byte)(decompressedSize >> 16);
            output[3] = (byte)(decompressedSize >> 8);
            output[4] = (byte)decompressedSize;
        }
    }
}
