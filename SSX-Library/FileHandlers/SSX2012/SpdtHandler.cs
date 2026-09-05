using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace SSXLibrary.FileHandlers.SSX2012
{
    // `.spdt` reader. Despite the extension and the vault registering both files
    // per track under `ge_spatialdata` as `_track_boundary` / `_track_helicopter`,
    // these are NOT splines -- there is no chunk table, no count-prefixed array
    // and no per-point record anywhere in the file. Each one is a 48-byte header
    // followed by a single regular 2D grid covering the track's XZ footprint at a
    // fixed 10 m cell pitch:
    //
    //     BNDY ('snowboun') -- u16 scalar field, bit15 set == out of bounds
    //     HEIG ('helicopt') -- f32 coarse terrain height (a DEM, not a camera path)
    //
    // NOTE: `.spdt` is LITTLE-endian, unlike `.geom` and the `.vlt` vault, which
    // are big-endian on the same PS3 disc. Reading this header big-endian gives
    // bbox floats around 1e33, which is the tell.
    //
    // This is a straight port of `ssx/ssx2012_spdt.py`, which is the verified
    // reference implementation (122 `.spdt` files off the retail disc, plus the
    // 4 that ship with the DLC tracks) -- see that file's module docstring for
    // the full format writeup. Keep this reader's behaviour in lockstep with it;
    // if they ever disagree, the Python side is the ground truth until proven
    // otherwise.
    public class SpdtHandler
    {
        public const int HeaderSize = 0x30;

        // BNDY only: the one bit of the u16 whose meaning is proven.
        public const int OobBit = 0x8000;

        // (maxX-minX)/width and (maxZ-minZ)/height come out at exactly this on
        // every file surveyed, so the bbox is the track footprint snapped out to
        // a 10 m lattice rather than a tight fit.
        public const float CellPitch = 10.0f;

        public string SourcePath = ""; // set by Load(string); empty via the stream overload
        public string Magic = "";       // "SDAT"
        public string Subtype = "";     // "BNDY" or "HEIG"
        public string TypeName = "";    // "snowboun" / "helicopt" -- NOT the track name
        public int Version;             // 1 on every file surveyed
        public Vector3 BboxMin;         // world space, Y-up, metres
        public Vector3 BboxMax;
        public int Width;               // cells along +X
        public int Height;              // cells along +Z

        public int Stride;              // payload bytes per cell, from Subtype
        public long FileSize;
        public long TailUnparsed;       // 0 on every file surveyed

        // Payload, row-major (Z outer, X inner). Only one of these is populated;
        // which one is decided by Subtype, so check IsBoundary / IsHeight first.
        public List<int> BoundaryCells = new List<int>();
        public List<float> HeightCells = new List<float>();

        public bool IsBoundary => Subtype == "BNDY";

        public bool IsHeight => Subtype == "HEIG";

        public int CellCount => Width * Height;

        /// <summary>(dx, dz) implied by the bbox and the grid dimensions.</summary>
        public Vector2 CellSize => new Vector2(
            (BboxMax.X - BboxMin.X) / Width,
            (BboxMax.Z - BboxMin.Z) / Height);

        /// <summary>
        /// HEIG's "nothing here" sentinel -- exactly the header's bbox min Y, which
        /// is why min(payload) == BboxMin.Y on every file surveyed.
        /// </summary>
        public float NoTerrain => BboxMin.Y;

        public void Load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                Load(stream);
            }
            SourcePath = path;
        }

        // Stream overload so a `.spdt` can be read straight out of an archive
        // member without staging it to disk. Everything is measured from the
        // stream's current position, not from 0.
        //
        // Pass `length` whenever the stream carries anything past the end of this
        // file -- an archive member with siblings behind it, say. Without it the
        // size is taken to EOF, those trailing bytes land in TailUnparsed, and
        // Check() reports them as unaccounted for.
        public void Load(Stream stream, long? length = null)
        {
            long start = stream.Position;
            long size = length ?? (stream.Length - start);
            if (size < HeaderSize)
            {
                throw new InvalidDataException("too short for a .spdt header");
            }

            // Everything below reads into locals and is committed to the instance
            // in one go at the end. Load() is public and re-callable on a live
            // handler, so a throw part way through must leave it on its previous
            // file rather than half one file and half another.

            // Strings go through the StreamExtensions reader rather than
            // StreamUtil.ReadString, which is [Obsolete] and points here; the
            // numeric reads below have no such replacement yet.
            string magic = stream.ReadAsciiWithLength(4, true);
            if (magic != "SDAT")
            {
                throw new InvalidDataException($"not a .spdt (magic '{magic}')");
            }

            string subtype = stream.ReadAsciiWithLength(4, true);
            int stride = StrideOf(subtype);

            // NUL-padded to 8 bytes, though both names in use fill it exactly.
            string typeName = stream.ReadAsciiWithLength(8, true);
            int version = StreamUtil.ReadUInt32(stream);
            Vector3 bboxMin = StreamUtil.ReadVector3(stream);
            Vector3 bboxMax = StreamUtil.ReadVector3(stream);
            int width = StreamUtil.ReadUInt16(stream);
            int height = StreamUtil.ReadUInt16(stream);

            long cells = (long)width * height;    // 0xffff^2 overflows an int
            long payload = size - HeaderSize;
            long tail = payload - cells * stride;
            if (tail < 0)
            {
                throw new InvalidDataException(
                    $"truncated payload: {width}x{height} cells need {cells * stride} bytes, {payload} present");
            }

            bool boundary = subtype == "BNDY";
            int n = (int)cells;                   // cells*stride <= payload, so this fits
            var boundaryCells = new List<int>(boundary ? n : 0);
            var heightCells = new List<float>(boundary ? 0 : n);
            for (int i = 0; i < n; i++)
            {
                if (boundary)
                {
                    boundaryCells.Add(StreamUtil.ReadUInt16(stream));
                }
                else
                {
                    heightCells.Add(StreamUtil.ReadFloat(stream));
                }
            }

            SourcePath = "";
            Magic = magic;
            Subtype = subtype;
            TypeName = typeName;
            Version = version;
            BboxMin = bboxMin;
            BboxMax = bboxMax;
            Width = width;
            Height = height;
            Stride = stride;
            FileSize = size;
            TailUnparsed = tail;
            BoundaryCells = boundaryCells;
            HeightCells = heightCells;
        }

        // Anything not in here is a hard error rather than a default stride: a
        // guessed stride would silently mis-length the payload and desync every
        // cell after the first row.
        private static int StrideOf(string subtype)
        {
            switch (subtype)
            {
                case "BNDY": return 2;
                case "HEIG": return 4;
                default:
                    throw new InvalidDataException($"unknown .spdt subtype '{subtype}'");
            }
        }

        // null for anything Load() would have rejected. The reference has no
        // equivalent state -- its constructor loads, so an unloaded Spdt cannot
        // exist -- but Check() here is public on a default-constructed handler.
        private static string? TypeNameOf(string subtype)
        {
            switch (subtype)
            {
                case "BNDY": return "snowboun";
                case "HEIG": return "helicopt";
                default: return null;
            }
        }

        // -- cell addressing --------------------------------------------------

        public int CellBoundary(int ix, int iz) => BoundaryCells[iz * Width + ix];

        public float CellHeight(int ix, int iz) => HeightCells[iz * Width + ix];

        // Both of these do their arithmetic in double, which is what the reference
        // uses throughout. In single precision the grid step is big enough to
        // swallow the offset near a cell boundary -- (-0.0001f) - (-2070f) rounds
        // to exactly 2070f -- and CellOf then hands back the next cell over.
        private double CellDx => ((double)BboxMax.X - BboxMin.X) / Width;

        private double CellDz => ((double)BboxMax.Z - BboxMin.Z) / Height;

        // Whether a cell samples the terrain at its centre or at its corner is
        // UNKNOWN -- the test against proxy vertices disagreed between tracks.
        // Centre is the default; pass corner: true for the other convention.
        public Vector2 WorldOf(int ix, int iz, bool corner = false)
        {
            double off = corner ? 0.0 : 0.5;
            return new Vector2(
                (float)(BboxMin.X + (ix + off) * CellDx),
                (float)(BboxMin.Z + (iz + off) * CellDz));
        }

        /// <summary>Inverse of <see cref="WorldOf"/>, clamped to the grid.</summary>
        public (int Ix, int Iz) CellOf(float x, float z, bool corner = false)
        {
            double off = corner ? 0.0 : 0.5;
            int ix = (int)(((double)x - BboxMin.X) / CellDx - off + 0.5);
            int iz = (int)(((double)z - BboxMin.Z) / CellDz - off + 0.5);
            return (Math.Min(Math.Max(ix, 0), Width - 1),
                    Math.Min(Math.Max(iz, 0), Height - 1));
        }

        // -- BNDY -------------------------------------------------------------

        // The low 15 bits are a smooth field that ramps across the boundary and
        // then plateaus, but its units and the meaning of its interior/exterior
        // plateau levels are UNKNOWN -- 2D/3D distance, height, slope, station
        // along the corridor and fp16 were each tested and refuted. Only bit15 is
        // decoded here, deliberately; don't invent a meaning for the rest.

        /// <summary>Out-of-bounds test at a world XZ. Nearest cell, no filtering.</summary>
        public bool IsOob(float x, float z)
        {
            RequireBoundary();
            // Written as a negated containment rather than four `<`/`>` tests so
            // that NaN is out of bounds, matching the reference's chained
            // `min <= x <= max`. Every ordered comparison against NaN is false.
            if (!(x >= BboxMin.X && x <= BboxMax.X && z >= BboxMin.Z && z <= BboxMax.Z))
            {
                return true;               // outside the grid is outside the track
            }
            (int ix, int iz) = CellOf(x, z);
            return (CellBoundary(ix, iz) & OobBit) != 0;
        }

        /// <summary>BNDY as row-major bools: true == out of bounds.</summary>
        public bool[] OobMask()
        {
            RequireBoundary();
            var mask = new bool[BoundaryCells.Count];
            for (int i = 0; i < BoundaryCells.Count; i++)
            {
                mask[i] = (BoundaryCells[i] & OobBit) != 0;
            }
            return mask;
        }

        /// <summary>
        /// Marching squares over the raw u16. The default threshold is the bit15
        /// flip, i.e. the in/out-of-bounds edge itself.
        /// </summary>
        public List<ContourSegment> ContourSegments(int threshold = OobBit)
        {
            RequireBoundary();
            var segs = new List<ContourSegment>();
            float t = threshold;

            for (int iz = 0; iz < Height - 1; iz++)
            {
                for (int ix = 0; ix < Width - 1; ix++)
                {
                    int v00 = BoundaryCells[iz * Width + ix];
                    int v10 = BoundaryCells[iz * Width + ix + 1];
                    int v01 = BoundaryCells[(iz + 1) * Width + ix];
                    int v11 = BoundaryCells[(iz + 1) * Width + ix + 1];

                    int code = (v00 >= t ? 1 : 0) | (v10 >= t ? 2 : 0)
                             | (v11 >= t ? 4 : 0) | (v01 >= t ? 8 : 0);
                    if (code == 0 || code == 15)
                    {
                        continue;
                    }

                    Vector2 c0 = WorldOf(ix, iz);
                    Vector2 c1 = WorldOf(ix + 1, iz + 1);
                    float x0 = c0.X, z0 = c0.Y, x1 = c1.X, z1 = c1.Y;

                    // edge crossings, named for the cell side they sit on
                    Vector2 top = Lerp(x0, z0, v00, x1, z0, v10, t);
                    Vector2 right = Lerp(x1, z0, v10, x1, z1, v11, t);
                    Vector2 bottom = Lerp(x0, z1, v01, x1, z1, v11, t);
                    Vector2 left = Lerp(x0, z0, v00, x0, z1, v01, t);

                    switch (code)
                    {
                        case 1: Add(segs, left, top); break;
                        case 2: Add(segs, top, right); break;
                        case 3: Add(segs, left, right); break;
                        case 4: Add(segs, right, bottom); break;
                        case 5: Add(segs, left, bottom); Add(segs, top, right); break;
                        case 6: Add(segs, top, bottom); break;
                        case 7: Add(segs, left, bottom); break;
                        case 8: Add(segs, left, bottom); break;
                        case 9: Add(segs, top, bottom); break;
                        case 10: Add(segs, left, top); Add(segs, right, bottom); break;
                        case 11: Add(segs, right, bottom); break;
                        case 12: Add(segs, left, right); break;
                        case 13: Add(segs, top, right); break;
                        case 14: Add(segs, left, top); break;
                    }
                }
            }
            return segs;
        }

        // double for the same reason as CellOf/WorldOf: the reference interpolates
        // in double, and the crossing fraction multiplies a 10 m span, so single
        // precision here moves the segment endpoint by up to a millimetre.
        private static Vector2 Lerp(float ax, float az, float av, float bx, float bz, float bv, float t)
        {
            double d = (double)bv - av;
            double f = d == 0 ? 0.5 : ((double)t - av) / d;
            return new Vector2(
                (float)(ax + ((double)bx - ax) * f),
                (float)(az + ((double)bz - az) * f));
        }

        private static void Add(List<ContourSegment> segs, Vector2 a, Vector2 b)
        {
            segs.Add(new ContourSegment { X0 = a.X, Z0 = a.Y, X1 = b.X, Z1 = b.Y });
        }

        // -- HEIG -------------------------------------------------------------

        /// <summary>
        /// Nearest-cell terrain height, or null where the sentinel says there is
        /// no terrain under that cell.
        /// </summary>
        public float? HeightAt(float x, float z)
        {
            RequireHeight();
            (int ix, int iz) = CellOf(x, z);
            float v = CellHeight(ix, iz);
            return v == NoTerrain ? (float?)null : v;
        }

        // -- self-check ---------------------------------------------------------

        /// <summary>
        /// Self-consistency assertions. Returns one string per problem; an empty
        /// list means the file agrees with everything the format survey proved.
        /// </summary>
        public List<string> Check()
        {
            var bad = new List<string>();
            if (Magic != "SDAT")
            {
                bad.Add($"magic '{Magic}' != 'SDAT'");
            }
            string? want = TypeNameOf(Subtype);
            if (want == null)
            {
                bad.Add($"unknown subtype '{Subtype}'");
            }
            else if (TypeName != want)
            {
                bad.Add($"type name '{TypeName}' != '{want}' for {Subtype}");
            }
            if (Version != 1)
            {
                bad.Add($"version {Version} != 1");
            }
            if (Width == 0 || Height == 0)
            {
                bad.Add($"degenerate dims {Width}x{Height}");
            }
            if (TailUnparsed != 0)
            {
                bad.Add($"{TailUnparsed} bytes unaccounted for (payload != w*h*{Stride})");
            }
            CheckAxis(bad, "x", BboxMin.X, BboxMax.X);
            CheckAxis(bad, "y", BboxMin.Y, BboxMax.Y);
            CheckAxis(bad, "z", BboxMin.Z, BboxMax.Z);

            Vector2 cell = CellSize;
            if (Math.Abs(cell.X - CellPitch) > 1e-6 || Math.Abs(cell.Y - CellPitch) > 1e-6)
            {
                bad.Add(FormattableString.Invariant(
                    $"cell size ({cell.X:F6}, {cell.Y:F6}) != {CellPitch:F1} m"));
            }

            if (IsHeight)
            {
                if (HeightCells.Count == 0)
                {
                    return bad;
                }
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (float v in HeightCells)
                {
                    lo = Math.Min(lo, v);
                    hi = Math.Max(hi, v);
                }
                // the sentinel IS bbox min Y, so the payload can never dip below it
                if (lo != BboxMin.Y)
                {
                    bad.Add(FormattableString.Invariant($"min height {lo:G} != bbox min Y {BboxMin.Y:G}"));
                }
                if (hi > BboxMax.Y + 1e-3)
                {
                    bad.Add(FormattableString.Invariant($"max height {hi:G} > bbox max Y {BboxMax.Y:G}"));
                }
            }
            else
            {
                if (BoundaryCells.Count == 0)
                {
                    return bad;
                }
                // Neither of these can fire while the payload is read as u16,
                // same as in the Python reference -- both are kept so a future
                // widening of the read is caught rather than silently
                // reinterpreted.
                foreach (int v in BoundaryCells)
                {
                    // bit15 must be exactly the >= 0x8000 partition (it is, cell
                    // for cell, on every file surveyed)
                    if (((v & OobBit) != 0) != (v >= OobBit))
                    {
                        bad.Add("bit15 disagrees with the >= 0x8000 partition");
                        break;
                    }
                }
                foreach (int v in BoundaryCells)
                {
                    if (v > 0xffff || v < 0)
                    {
                        bad.Add("u16 payload out of range");
                        break;
                    }
                }
            }
            return bad;
        }

        private static void CheckAxis(List<string> bad, string axis, float min, float max)
        {
            if (min > max)
            {
                bad.Add(FormattableString.Invariant($"bbox {axis} min {min:G} > max {max:G}"));
            }
        }

        private void RequireBoundary()
        {
            if (!IsBoundary)
            {
                throw new InvalidOperationException($"needs a BNDY file, got '{Subtype}'");
            }
        }

        private void RequireHeight()
        {
            if (!IsHeight)
            {
                throw new InvalidOperationException($"needs a HEIG file, got '{Subtype}'");
            }
        }

        // -- export -------------------------------------------------------------

        // HEIG -> a quad mesh over the cells that have terrain (the sentinel cells
        // are left as holes rather than flattened to the bbox floor). BNDY -> the
        // bit15 contour as line segments, laid flat at bbox min Y since the file
        // carries no height of its own.
        public void ExportObj(string path)
        {
            var output = new StringBuilder();
            output.Append("# ").Append(Path.GetFileName(SourcePath)).Append("  ").Append(Subtype).Append('\n');

            if (IsBoundary)
            {
                List<ContourSegment> segs = ContourSegments();
                float y = BboxMin.Y;
                foreach (ContourSegment s in segs)
                {
                    AppendVertex(output, s.X0, y, s.Z0);
                    AppendVertex(output, s.X1, y, s.Z1);
                }
                output.Append("o boundary\n");
                for (int i = 0; i < segs.Count; i++)
                {
                    output.Append("l ").Append(2 * i + 1).Append(' ').Append(2 * i + 2).Append('\n');
                }
            }
            else
            {
                var idx = new int[CellCount];
                int next = 0;
                for (int iz = 0; iz < Height; iz++)
                {
                    for (int ix = 0; ix < Width; ix++)
                    {
                        float v = CellHeight(ix, iz);
                        if (v == NoTerrain)
                        {
                            continue;      // 0 == "no vertex here", indices are 1-based
                        }
                        Vector2 w = WorldOf(ix, iz);
                        idx[iz * Width + ix] = ++next;
                        AppendVertex(output, w.X, v, w.Y);
                    }
                }
                output.Append("o terrain\n");
                for (int iz = 0; iz < Height - 1; iz++)
                {
                    for (int ix = 0; ix < Width - 1; ix++)
                    {
                        int a = idx[iz * Width + ix];
                        int b = idx[iz * Width + ix + 1];
                        int c = idx[(iz + 1) * Width + ix + 1];
                        int d = idx[(iz + 1) * Width + ix];
                        if (a == 0 || b == 0 || c == 0 || d == 0)
                        {
                            continue;
                        }
                        output.Append("f ").Append(a).Append(' ').Append(b).Append(' ')
                              .Append(c).Append(' ').Append(d).Append('\n');
                    }
                }
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.WriteAllText(path, output.ToString());
        }

        // "F3", not the custom "0.000": custom numeric formats round a float
        // through a 7-significant-digit intermediate first, so 131.03148f prints
        // as 131.032 where the true 3-decimal rounding is 131.031. The error is
        // invisible at track-sized coordinates and grows with magnitude.
        private static void AppendVertex(StringBuilder output, float x, float y, float z)
        {
            output.Append("v ")
                  .Append(x.ToString("F3", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(y.ToString("F3", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(z.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
        }

        // -- structs ------------------------------------------------------------

        public struct ContourSegment
        {
            public float X0;
            public float Z0;
            public float X1;
            public float Z1;

            public override string ToString() => FormattableString.Invariant(
                $"({X0:F3}, {Z0:F3}) -> ({X1:F3}, {Z1:F3})");
        }
    }
}
