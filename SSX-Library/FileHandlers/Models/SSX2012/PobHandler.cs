using SSX_Library.Internal.Utilities.StreamExtensions;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SSXLibrary.FileHandlers.Models.SSX2012
{
    // `.pob` reader -- the prop collision proxies. `<prop>_physicsobject.pob` is
    // one prop's rigid-body collision definition.
    //
    // The container is Criterion **RenderWare Collision** ("rwc") serialised as an
    // *Assembly*: a small object table followed by the objects themselves laid out
    // back to back, with every inter-object pointer stored as a plain FILE OFFSET
    // -- load the file at base B, add B, done. Free space between objects is
    // filled with the byte 0xDD.
    //
    // NOTE: `.pob` is the only LITTLE-endian format on the disc. `.geom`, `.subd`,
    // the `.vlt` vault and the ghost recordings are all big-endian. Reading
    // `u32 @0x08 == filesize` as LE is the cheapest confirmation.
    //
    // Layout, in the order the file lays it out:
    //
    //     0x00  u32   magic 0xB8EF44FE
    //     0x04  u32   version (1 on every file surveyed)
    //     0x08  u32   file size, exact
    //     0x0c  u32   alignment (16 on every file surveyed)
    //     0x10  u32   nSections
    //     0x14  nSections * u32   ascending absolute file offsets; [0] is the TOC
    //
    //     TOC: two counts, a type table, an object table and a string table. The
    //     typeId is a stable class id, constant across the corpus: 1
    //     SimpleMappedArray, 2 Volume, 3 ClusteredMesh, 4 BitTable, 5 Assembly
    //     Definition, 6 Assembly. The TypeDescs are NOT sorted by id.
    //
    //     Volume (96 B)          a transform plus either an aggregate pointer
    //                            (volumeType 6) or a two-parameter primitive
    //                            (volumeType 5: half height along local Z, radius)
    //     SimpleMappedArray      an AABB and the volume array
    //     ClusteredMesh          bbox, an embedded KD-tree and the clusters --
    //                            99% of what a `.pob` contains
    //     Assembly Definition    rigid-body mass properties, NOT geometry
    //     Assembly BitTable      4 u32, constant
    //     Assembly               the root record
    //
    // This is a straight port of `ssx/ssx2012_pob.py`, which is the verified
    // reference implementation (255 `.pob` files off the retail disc) -- see that
    // file's module docstring for the full format writeup, the surfaceID census
    // and the unknowns. Keep this reader's behaviour in lockstep with it; if they
    // ever disagree, the Python side is the ground truth until proven otherwise.
    //
    // Unlike the other handlers here this one parses out of a byte[] rather than
    // walking a Stream. That is not a style preference: the format is a pointer
    // graph over the whole file, objects are reached by absolute offset in an
    // order unrelated to their layout, and several records are read twice from
    // different directions. Seeking a Stream around to do that would be slower and
    // much easier to get wrong.
    public class PobHandler
    {
        public const uint Magic = 0xB8EF44FE;

        // Inter-object free space. Every hole in a well-formed file is filled with
        // this, which is what makes the 100% byte accounting in Check() possible.
        public const byte Filler = 0xDD;

        public const int VolumeAggregate = 6;
        public const int VolumePrimitive2 = 5;

        public string SourcePath = ""; // set by Load(string); empty via the stream overload

        public byte[] Data = Array.Empty<byte>();

        public uint MagicValue;
        public int Version;
        public int DeclaredSize;
        public int Alignment;
        public int SectionCount;

        // Absolute file offsets, ascending. [0] is the TOC; the rest are the
        // objects, in the same order as the object table once sorted.
        public List<int> Sections = new List<int>();

        public int TocOffset;
        public int TocC0;             // 3 on every file surveyed; meaning unknown
        public int TypeCount;         // 6, or 5 when the prop has no mesh
        public int ObjectCount;
        public int StringBytes;
        public int TypeTableOffset;   // all three are relative to the TOC
        public int ObjectTableOffset;
        public int StringTableOffset;

        public List<TypeDescStruct> Types = new List<TypeDescStruct>();
        public List<ObjectEntryStruct> Objects = new List<ObjectEntryStruct>();

        // The root of the volume tree: one aggregate Volume pointing at the
        // SimpleMappedArray that holds the rest.
        public VolumeStruct Root = new VolumeStruct();

        public int ArrayOffset;
        public Vector3 ArrayBboxMin;
        public Vector3 ArrayBboxMax;
        public int VolumeCount;
        public int VolumeArrayOffset;

        public List<VolumeStruct> Volumes = new List<VolumeStruct>();

        // 0, 1 or 2 of them. A prop only carries two so the halves can differ in
        // groupID/surfaceID; 22/22 two-mesh files surveyed do.
        public List<ClusteredMeshStruct> Meshes = new List<ClusteredMeshStruct>();

        public int DefinitionOffset;
        public int BitTableOffset;
        public int AssemblyOffset;

        // INVERSE inertia tensor diagonal for unit mass. Derived rather than
        // asserted: on the four cylinder props it matches 1/I of a solid cylinder
        // to four significant figures.
        public Vector3 InverseInertia;
        public float DefinitionU50;   // 1.0 on every file surveyed
        public float DefinitionU54;   // 3.9958e31 on every file surveyed
        public float DefinitionMass;  // 100.0 on every file surveyed; "mass" is a guess

        // The inertia frame, 3 basis rows then translation. The translation row is
        // MINUS the centre of mass -- verified on the four cylinder props, where
        // it is exactly -(volume translation).
        public Vector3[] InertiaFrame = new Vector3[4];

        public Vector3 NegativeCentreOfMass => InertiaFrame[3];

        // (0.2, 0.2, 0.1) in all 255 files. Material constants of some kind; not
        // decoded, deliberately exposed raw.
        public Vector3 DefinitionU_B0;

        public void Load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                Load(stream);
            }
            SourcePath = path;
        }

        // Stream overload so a `.pob` can be read straight out of an archive member
        // without staging it to disk. Everything is measured from the stream's
        // current position, not from 0.
        //
        // Pass `length` whenever the stream carries anything past the end of this
        // file. Without it the size is taken to EOF and the trailing bytes are
        // reported by Check() as a mismatch against the header's own size field.
        public void Load(Stream stream)
        {
            Reader.SetDefaultReadMode(SSX_Library.ByteOrder.LittleEndian);


            // Everything below parses into locals and is committed to the instance
            // in one go at the end. Load() is public and re-callable on a live
            // handler, so a throw part way through must leave it on its previous
            // file rather than half one file and half another.
            uint magic = stream.ReadUInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException($"bad magic 0x{magic:X8}");
            }

            int version = (int)stream.ReadUInt32();
            int declaredSize = (int)stream.ReadUInt32();
            int alignment = (int)stream.ReadUInt32();
            int sectionCount = (int)stream.ReadUInt32();
            if (sectionCount < 1 || 20 + 4L * sectionCount > stream.Length)
            {
                throw new InvalidDataException($"section count {sectionCount} does not fit the file");
            }

            var sections = new List<int>(sectionCount);
            for (int i = 0; i < sectionCount; i++)
            {
                sections.Add((int)stream.ReadUInt32());
            }

            int toc = sections[0];
            stream.Position = toc;

            int tocC0 = (int)stream.ReadUInt32();
            int typeCount = (int)stream.ReadUInt32();
            int objectCount = (int)stream.ReadUInt32();
            int stringBytes = (int)stream.ReadUInt32();
            int typeTable = (int)stream.ReadUInt32();
            int objectTable = (int)stream.ReadUInt32();
            int stringTable = (int)stream.ReadUInt32();

            var types = new List<TypeDescStruct>(typeCount);
            stream.Position = toc + typeTable;
            for (int i = 0; i < typeCount; i++)
            {
                types.Add(new TypeDescStruct
                {
                    TypeId = (int)stream.ReadUInt32(),
                    Count = (int)stream.ReadUInt32(),
                    Offset = (int)stream.ReadUInt32(),
                });
            }

            var objects = new List<ObjectEntryStruct>(objectCount);
            stream.Position= toc+objectTable;
            for (int i = 0; i < objectCount; i++)
            {
                var objectEntry = new ObjectEntryStruct();

                objectEntry.Offset = (int)stream.ReadUInt32();

                long CurrentPos = stream.Position;
                stream.Position = toc + stream.ReadUInt32();
                objectEntry.Name = stream.ReadAsciiNullTerminated();
                stream.Position = CurrentPos;

                objects.Add(objectEntry);
            }

            // Objects are looked up by name -- the names are fixed across the
            // corpus (see the reference's docstring) and are the only stable way
            // in, since the type table's order is not.
            int rootOffset = RequireObject(objects, "rwcAggregateNode1");
            VolumeStruct root = ReadVolume(data, rootOffset);

            int arrayOffset = RequireObject(objects, "rwcAggregateNode1_SimpleMappedArray");
            Vector3 arrayMin = Vec3(data, arrayOffset);
            Vector3 arrayMax = Vec3(data, arrayOffset + 0x10);
            int volumeCount = I32(data, arrayOffset + 0x24);
            int volumeArrayOffset = arrayOffset + I32(data, arrayOffset + 0x30);

            var volumes = new List<VolumeStruct>(Math.Max(volumeCount, 0));
            for (int i = 0; i < volumeCount; i++)
            {
                volumes.Add(ReadVolume(data, volumeArrayOffset + VolumeStruct.Size * i));
            }

            var meshes = new List<ClusteredMeshStruct>();
            foreach (ObjectEntryStruct entry in objects)
            {
                if (entry.Name.Contains("ClusteredMesh", StringComparison.Ordinal))
                {
                    meshes.Add(ReadClusteredMesh(data, entry.Offset, entry.Name));
                }
            }

            int definitionOffset = RequireObject(objects, "Assembly Definition");
            int bitTableOffset = RequireObject(objects, "Assembly BitTable");
            int assemblyOffset = RequireObject(objects, "Assembly");

            var inertiaFrame = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                inertiaFrame[i] = Vec3(data, definitionOffset + 0x70 + 16 * i);
            }

            Data = data;
            SourcePath = "";
            MagicValue = magic;
            Version = version;
            DeclaredSize = declaredSize;
            Alignment = alignment;
            SectionCount = sectionCount;
            Sections = sections;
            TocOffset = toc;
            TocC0 = tocC0;
            TypeCount = typeCount;
            ObjectCount = objectCount;
            StringBytes = stringBytes;
            TypeTableOffset = typeTable;
            ObjectTableOffset = objectTable;
            StringTableOffset = stringTable;
            Types = types;
            Objects = objects;
            Root = root;
            ArrayOffset = arrayOffset;
            ArrayBboxMin = arrayMin;
            ArrayBboxMax = arrayMax;
            VolumeCount = volumeCount;
            VolumeArrayOffset = volumeArrayOffset;
            Volumes = volumes;
            Meshes = meshes;
            DefinitionOffset = definitionOffset;
            BitTableOffset = bitTableOffset;
            AssemblyOffset = assemblyOffset;
            InverseInertia = Vec3(data, definitionOffset + 0x44);
            DefinitionU50 = F32(data, definitionOffset + 0x50);
            DefinitionU54 = F32(data, definitionOffset + 0x54);
            DefinitionMass = F32(data, definitionOffset + 0x58);
            InertiaFrame = inertiaFrame;
            DefinitionU_B0 = Vec3(data, definitionOffset + 0xB0);
        }

        // -- record readers ---------------------------------------------------

        private static VolumeStruct ReadVolume(Stream d, int off)
        {
            d.Position = off;
            var v = new VolumeStruct
            {
                Offset = off,
                Rows = new Vector3[4],
                VolumeType = I32(d, off + 0x40),
                Fatness = F32(d, off + 0x50),
                GroupId = I32(d, off + 0x54),
                SurfaceId = I32(d, off + 0x58),
                Flags = I32(d, off + 0x5C),
            };
            for (int i = 0; i < 4; i++)
            {
                v.Rows[i] = Vec3(d, off + 16 * i);
            }

            // +0x44 is a union: an aggregate pointer for type 6, the first of two
            // primitive parameters for type 5. Reading both would put 0xDDDDDDDD
            // in one of them, so only the live arm is decoded.
            if (v.VolumeType == VolumeAggregate)
            {
                v.Aggregate = I32(d, off + 0x44);
            }
            else if (v.VolumeType == VolumePrimitive2)
            {
                v.HalfHeight = F32(d, off + 0x44);
                v.Radius = F32(d, off + 0x48);
            }
            return v;
        }

        private static ClusteredMeshStruct ReadClusteredMesh(byte[] d, int off, string name)
        {
            var m = new ClusteredMeshStruct
            {
                Offset = off,
                Name = name,
                // The EXACT float bbox. The quantised cluster vertices can exceed
                // it by up to one granularity (0.04) and never more -- measured
                // max 0.0399 -- so Check() tests them against bbox +/- granularity.
                BboxMin = Vec3(d, off),
                BboxMax = Vec3(d, off + 0x10),
                TagBits = I32(d, off + 0x24),
                UnitCount = I32(d, off + 0x28),
                KdTreeOffset = I32(d, off + 0x30),
                ClusterTableOffset = I32(d, off + 0x34),
                Granularity = F32(d, off + 0x38),
                ClusterFlags = U16(d, off + 0x3C),
                GroupIdSize = d[off + 0x3E],
                SurfaceIdSize = d[off + 0x3F],
                ClusterCount = I32(d, off + 0x40),
                Size = I32(d, off + 0x50),
                U58 = d[off + 0x58],
                ClusterBits = I32(d, off + 0x5C),
            };

            int k = off + 0x60;
            m.KdNodesOffset = U32(d, k);
            m.KdBranchCount = I32(d, k + 4);
            m.KdEntryCount = I32(d, k + 8);
            m.KdBboxMin = Vec3(d, k + 0x10);
            m.KdBboxMax = Vec3(d, k + 0x20);

            m.BranchNodes = new List<BranchNodeStruct>(Math.Max(m.KdBranchCount, 0));
            for (int i = 0; i < m.KdBranchCount; i++)
            {
                int o = off + 0xA0 + 32 * i;
                m.BranchNodes.Add(new BranchNodeStruct
                {
                    Parent = I32(d, o),
                    Axis = I32(d, o + 4),
                    Content0 = U32(d, o + 8),
                    Index0 = U32(d, o + 12),
                    Content1 = U32(d, o + 16),
                    Index1 = U32(d, o + 20),
                    Extent0 = F32(d, o + 24),
                    Extent1 = F32(d, o + 28),
                });
            }

            int ct = off + m.ClusterTableOffset;
            m.ClusterTable = ct;
            m.ClusterOffsets = new List<int>(Math.Max(m.ClusterCount, 0));
            for (int i = 0; i < m.ClusterCount; i++)
            {
                m.ClusterOffsets.Add(I32(d, ct + 4 * i));
            }

            m.Clusters = new List<ClusterStruct>(m.ClusterOffsets.Count);
            foreach (int o in m.ClusterOffsets)
            {
                // Cluster offsets are relative to the table itself, not the object.
                m.Clusters.Add(ReadCluster(d, ct + o, m.Granularity));
            }
            return m;
        }

        private static ClusterStruct ReadCluster(byte[] d, int off, float granularity)
        {
            var c = new ClusterStruct
            {
                Offset = off,
                UnitCount = U16(d, off),
                UnitDataSize = U16(d, off + 0x02),
                UnitDataStart = U16(d, off + 0x04),
                NormalStart = U16(d, off + 0x06),
                TotalSize = U16(d, off + 0x08),
                VertexCount = d[off + 0x0A],
                NormalCount = d[off + 0x0B],
                Compression = d[off + 0x0C],
            };
            if (c.Compression != 1)
            {
                throw new InvalidDataException(
                    $"cluster @0x{off:x}: compression mode {c.Compression} not seen on the disc");
            }

            int ox = I32(d, off + 0x10);
            int oy = I32(d, off + 0x14);
            int oz = I32(d, off + 0x18);
            c.Origin = new QuantisedVertex { X = ox, Y = oy, Z = oz };

            c.QuantisedVertices = new List<QuantisedVertex>(c.VertexCount);
            c.Vertices = new List<Vector3>(c.VertexCount);
            for (int i = 0; i < c.VertexCount; i++)
            {
                int o = off + 0x1C + 6 * i;
                var q = new QuantisedVertex
                {
                    X = ox + U16(d, o),
                    Y = oy + U16(d, o + 2),
                    Z = oz + U16(d, o + 4),
                };
                c.QuantisedVertices.Add(q);
                // double, then narrowed: the reference multiplies in double, and
                // an int * float product rounds differently from the same product
                // taken in double and rounded once at the end.
                c.Vertices.Add(new Vector3(
                    (float)(q.X * (double)granularity),
                    (float)(q.Y * (double)granularity),
                    (float)(q.Z * (double)granularity)));
            }
            c.VertexBytes = 12 + 6 * c.VertexCount;

            // The unit stream starts at a 16-byte multiple past the header, so
            // vertices are followed by 0xDD padding when they do not fill it.
            c.UnitOffset = off + 16 + 16 * c.UnitDataStart;
            c.Units = new List<UnitStruct>(c.UnitCount);
            int q2 = c.UnitOffset;
            for (int i = 0; i < c.UnitCount; i++)
            {
                byte t = d[q2];
                int sides = SidesOf(t);
                if (sides == 0)
                {
                    throw new InvalidDataException($"cluster @0x{off:x}: unit type 0x{t:x2}");
                }

                var u = new UnitStruct
                {
                    ByteOffset = q2 - c.UnitOffset,
                    TypeByte = t,
                    Indices = new byte[sides],
                };
                Array.Copy(d, q2 + 1, u.Indices, 0, sides);
                q2 += 1 + sides;

                // Bit 0x20 is set on every unit on the disc, so the per-edge byte
                // array is always present in practice -- but it is a flag, so it
                // is read as one.
                u.EdgeBytes = (t & 0x20) != 0 ? new byte[sides] : Array.Empty<byte>();
                if (u.EdgeBytes.Length > 0)
                {
                    Array.Copy(d, q2, u.EdgeBytes, 0, sides);
                }
                q2 += u.EdgeBytes.Length;

                c.Units.Add(u);
            }
            c.UnitBytes = q2 - c.UnitOffset;
            return c;
        }

        // Low nibble of the unit type byte. 0 means "not a shape we know", which
        // the caller turns into a throw rather than a silent skip -- a skipped unit
        // desyncs the variable-stride walk for every unit behind it.
        private static int SidesOf(byte typeByte)
        {
            switch (typeByte & 0x0F)
            {
                case 1: return 3;   // triangle
                case 2: return 4;   // quad
                default: return 0;
            }
        }

        private static int RequireObject(List<ObjectEntryStruct> objects, string name)
        {
            foreach (ObjectEntryStruct e in objects)
            {
                if (e.Name == name)
                {
                    return e.Offset;
                }
            }
            throw new InvalidDataException($"no '{name}' object in the TOC");
        }

        // -- geometry ---------------------------------------------------------

        /// <summary>
        /// Collision triangles in prop-local space, each volume's transform
        /// applied. Quads are split (0,1,2) + (1,3,2), which is verified against
        /// the matching render meshes.
        /// </summary>
        public List<Triangle> Tris()
        {
            var output = new List<Triangle>();
            foreach (VolumeStruct v in Volumes)
            {
                if (v.VolumeType != VolumeAggregate)
                {
                    continue;
                }
                ClusteredMeshStruct? mesh = MeshAt(v.Aggregate);
                if (mesh == null)
                {
                    continue;
                }
                foreach (Triangle t in mesh.Tris())
                {
                    output.Add(new Triangle
                    {
                        A = Transform(v, t.A),
                        B = Transform(v, t.B),
                        C = Transform(v, t.C),
                    });
                }
            }
            return output;
        }

        /// <summary>The SimpleMappedArray's declared AABB, in prop-local space.</summary>
        public (Vector3 Min, Vector3 Max) Bbox() => (ArrayBboxMin, ArrayBboxMax);

        /// <summary>The ClusteredMesh a volume's aggregate pointer resolves to, or null.</summary>
        public ClusteredMeshStruct? MeshAt(int offset)
        {
            foreach (ClusteredMeshStruct m in Meshes)
            {
                if (m.Offset == offset)
                {
                    return m;
                }
            }
            return null;
        }

        /// <summary>Apply a volume's transform to a point, narrowed to float.</summary>
        public static Vector3 Transform(VolumeStruct v, Vector3 p)
        {
            (double x, double y, double z) = TransformDouble(v, p.X, p.Y, p.Z);
            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>
        /// The same transform in full double precision. The reference has no
        /// single-precision type, so every intermediate there is a double; this is
        /// the entry point to use when that residue matters. <see cref="Transform"/>
        /// narrows the result to <see cref="Vector3"/> for house-style consistency,
        /// which costs up to a rounding step at prop scale.
        /// </summary>
        public static (double X, double Y, double Z) TransformDouble(VolumeStruct v, double px, double py, double pz)
        {
            Vector3[] r = v.Rows;
            return (
                px * r[0].X + py * r[1].X + pz * r[2].X + r[3].X,
                px * r[0].Y + py * r[1].Y + pz * r[2].Y + r[3].Y,
                px * r[0].Z + py * r[1].Z + pz * r[2].Z + r[3].Z);
        }

        // -- byte accounting --------------------------------------------------

        /// <summary>
        /// Spans covering every byte of the file, in ascending order and with no
        /// gaps. Holes between the records are labelled either as 0xDD filler or,
        /// if they hold anything else, as UNEXPLAINED -- which is what makes this
        /// a decode test rather than a pretty-printer. Check() reads it as one.
        /// </summary>
        public List<BlockSpan> BlockMap()
        {
            byte[] d = Data;
            var sp = new List<BlockSpan>
            {
                new BlockSpan(0, 20, "file header"),
                new BlockSpan(20, 20 + 4 * SectionCount, "section offset table"),
                new BlockSpan(TocOffset, TocOffset + 28, "TOC header"),
                new BlockSpan(TocOffset + TypeTableOffset,
                              TocOffset + TypeTableOffset + 12 * TypeCount, "TOC type table"),
                new BlockSpan(TocOffset + ObjectTableOffset,
                              TocOffset + ObjectTableOffset + 8 * ObjectCount, "TOC object table"),
                new BlockSpan(TocOffset + StringTableOffset,
                              TocOffset + StringTableOffset + StringBytes, "TOC string table"),
                new BlockSpan(Root.Offset, Root.Offset + VolumeStruct.Size, "Volume rwcAggregateNode1"),
                new BlockSpan(ArrayOffset, ArrayOffset + 0x40, "SimpleMappedArray header"),
                new BlockSpan(VolumeArrayOffset,
                              VolumeArrayOffset + VolumeStruct.Size * VolumeCount,
                              FormattableString.Invariant($"SimpleMappedArray volumes ({VolumeCount})")),
            };

            foreach (ClusteredMeshStruct m in Meshes)
            {
                sp.Add(new BlockSpan(m.Offset, m.Offset + 0x60, $"{m.Name} header"));
                sp.Add(new BlockSpan(m.Offset + 0x60, m.Offset + 0xA0, $"{m.Name} KDTree"));
                if (m.KdBranchCount != 0)
                {
                    sp.Add(new BlockSpan(m.Offset + 0xA0, m.Offset + 0xA0 + 32 * m.KdBranchCount,
                        FormattableString.Invariant($"{m.Name} KDTree branch nodes ({m.KdBranchCount})")));
                }
                sp.Add(new BlockSpan(m.ClusterTable, m.ClusterTable + 4 * m.ClusterCount,
                    FormattableString.Invariant($"{m.Name} cluster table ({m.ClusterCount})")));
                for (int i = 0; i < m.Clusters.Count; i++)
                {
                    ClusterStruct c = m.Clusters[i];
                    sp.Add(new BlockSpan(c.Offset, c.Offset + 16,
                        FormattableString.Invariant($"{m.Name} cluster {i} header")));
                    sp.Add(new BlockSpan(c.Offset + 16, c.Offset + 16 + c.VertexBytes,
                        FormattableString.Invariant($"{m.Name} cluster {i} verts ({c.VertexCount})")));
                    sp.Add(new BlockSpan(c.UnitOffset, c.UnitOffset + c.UnitBytes,
                        FormattableString.Invariant($"{m.Name} cluster {i} units ({c.UnitCount})")));
                }
            }

            sp.Add(new BlockSpan(DefinitionOffset, DefinitionOffset + 212, "Assembly Definition"));
            sp.Add(new BlockSpan(BitTableOffset, BitTableOffset + 28, "Assembly BitTable"));
            sp.Add(new BlockSpan(AssemblyOffset, AssemblyOffset + 144, "Assembly"));

            // Stable sort by (start, end, label), matching the reference's tuple
            // sort -- two spans starting at the same byte must not swap places.
            sp.Sort((a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) { return c; }
                c = a.End.CompareTo(b.End);
                return c != 0 ? c : string.CompareOrdinal(a.Label, b.Label);
            });

            var output = new List<BlockSpan>(sp.Count);
            int cur = 0;
            foreach (BlockSpan s in sp)
            {
                if (s.Start > cur)
                {
                    output.Add(new BlockSpan(cur, s.Start, FillerLabel(d, cur, s.Start)));
                }
                output.Add(s);
                cur = Math.Max(cur, s.End);
            }
            if (cur < d.Length)
            {
                output.Add(new BlockSpan(cur, d.Length, FillerLabel(d, cur, d.Length)));
            }
            return output;
        }

        private static string FillerLabel(byte[] d, int a, int b)
        {
            for (int i = a; i < b; i++)
            {
                if (d[i] != Filler)
                {
                    return "UNEXPLAINED";
                }
            }
            return "0xDD filler";
        }

        // -- self-check -------------------------------------------------------

        /// <summary>
        /// Assertions a wrong decode would break. Returns one string per problem;
        /// an empty list means the file agrees with everything the format survey
        /// proved. Cheap to run and worth running -- several of these have teeth,
        /// e.g. shifting the 96-byte volume stride by one word turns surfaceID
        /// into garbage and the surfaceID table catches it.
        /// </summary>
        public List<string> Check()
        {
            var bad = new List<string>();
            byte[] d = Data;
            int n = d.Length;

            void Want(bool cond, string msg)
            {
                if (!cond) { bad.Add(msg); }
            }

            Want(Version == 1, $"version {Version} != 1");
            Want(DeclaredSize == n, $"header size {DeclaredSize} != filesize {n}");
            Want(Alignment == 16, $"alignment {Alignment} != 16");

            bool ascending = true;
            for (int i = 1; i < Sections.Count; i++)
            {
                if (Sections[i] < Sections[i - 1]) { ascending = false; break; }
            }
            Want(ascending, "section offsets not ascending");
            Want(Sections.Count > 0 && Sections[Sections.Count - 1] < n, "last section offset past EOF");
            Want(SectionCount == ObjectCount + 1,
                 $"nSections {SectionCount} != nObjects+1 {ObjectCount + 1}");

            var objectOffsets = new List<int>(Objects.Count);
            foreach (ObjectEntryStruct e in Objects) { objectOffsets.Add(e.Offset); }
            objectOffsets.Sort();
            Want(SameInts(objectOffsets, Sections.GetRange(1, Sections.Count - 1)),
                 "object offsets do not match the section table");

            int typeTotal = 0;
            foreach (TypeDescStruct t in Types) { typeTotal += t.Count; }
            Want(typeTotal == ObjectCount, "type counts do not sum to nObjects");

            var tids = new HashSet<int>();
            bool tidsOk = true;
            foreach (TypeDescStruct t in Types)
            {
                if (!tids.Add(t.TypeId) || t.TypeId < 1 || t.TypeId > 6) { tidsOk = false; }
            }
            Want(tidsOk, "type ids are not distinct values in 1..6");

            var names = new List<string>(Objects.Count);
            foreach (ObjectEntryStruct e in Objects) { names.Add(e.Name); }
            Want(new HashSet<string>(names).Count == names.Count, "duplicate object names");

            // typeId -> class is a fixed bijection, and the TypeDescs point at
            // contiguous runs of the object table in file order.
            int k2 = 0;
            foreach (TypeDescStruct t in Types)
            {
                Want(t.Offset == ObjectTableOffset + 8 * k2,
                     $"type {t.TypeId} points at object entry {(t.Offset - ObjectTableOffset) / 8}, expected {k2}");
                string? expect = ClassOfTypeId(t.TypeId);
                for (int i = 0; i < t.Count; i++)
                {
                    if (k2 >= names.Count)
                    {
                        bad.Add("type counts run past the object table");
                        break;
                    }
                    bool ok = expect != null &&
                              (IsExactNameType(t.TypeId)
                                  ? names[k2] == expect
                                  : names[k2].Contains(expect, StringComparison.Ordinal));
                    Want(ok, $"typeId {t.TypeId} maps to '{names[k2]}', expected {expect ?? "?"}");
                    k2++;
                }
            }

            int sz = 0;
            foreach (string nm in names) { sz += nm.Length + 1; }
            Want(sz == StringBytes, $"string table {StringBytes} != sum of name lengths {sz}");
            Want(TocC0 == 3, $"TOC[0] {TocC0} != 3");
            Want(TypeTableOffset == 28, $"type table offset {TypeTableOffset} != 28");

            // The root volume points at the SimpleMappedArray; each array volume
            // points at a mesh or is a primitive.
            Want(Root.VolumeType == VolumeAggregate,
                 $"root volume type {Root.VolumeType} != {VolumeAggregate}");
            Want(Root.Aggregate == ArrayOffset, "root volume does not point at the SimpleMappedArray");
            Want(VolumeCount >= 1, "SimpleMappedArray is empty");

            var seen = new HashSet<int>();
            for (int i = 0; i < Volumes.Count; i++)
            {
                VolumeStruct v = Volumes[i];
                if (v.VolumeType == VolumeAggregate)
                {
                    Want(MeshAt(v.Aggregate) != null,
                         $"volume {i} points at 0x{v.Aggregate:x}, not a ClusteredMesh");
                    seen.Add(v.Aggregate);
                }
                else if (v.VolumeType == VolumePrimitive2)
                {
                    Want(v.HalfHeight > 0 && v.Radius > 0,
                         $"volume {i} primitive has a non-positive parameter");
                }
                else
                {
                    bad.Add($"volume {i} has unknown volumeType {v.VolumeType}");
                }
                Want(v.Fatness == 0.0f,
                     FormattableString.Invariant($"volume {i} fatness {v.Fatness:G} != 0"));
                Want(IsKnownSurfaceId(v.SurfaceId),
                     $"volume {i} surfaceID {v.SurfaceId} is outside the corpus table");
                Want(v.GroupId == 0 || v.GroupId == 1 || v.GroupId == 2 || v.GroupId == 11,
                     $"volume {i} groupID {v.GroupId} is outside the corpus table");
                Want(v.Flags == 0 || v.Flags == 1, $"volume {i} flags {v.Flags} not in (0, 1)");
            }

            var meshOffsets = new HashSet<int>();
            foreach (ClusteredMeshStruct m in Meshes) { meshOffsets.Add(m.Offset); }
            Want(seen.SetEquals(meshOffsets), "a ClusteredMesh is not referenced by a volume");

            if (Volumes.Count == 2 && Meshes.Count == 2)
            {
                // A prop only carries two meshes so the halves can differ; all 22
                // two-mesh files surveyed differ in (groupID, surfaceID).
                VolumeStruct a = Volumes[0], b = Volumes[1];
                Want(a.GroupId != b.GroupId || a.SurfaceId != b.SurfaceId,
                     "the two volumes of a two-mesh prop share (groupID, surfaceID)");
            }

            CheckArrayBbox(bad);

            foreach (ClusteredMeshStruct m in Meshes)
            {
                CheckMesh(bad, m);
            }

            // the trailing records
            Want(U32(d, BitTableOffset) == 1 && U32(d, BitTableOffset + 4) == 1
                 && U32(d, BitTableOffset + 8) == 1 && U32(d, BitTableOffset + 12) == 0,
                 "Assembly BitTable is not (1,1,1,0)");
            Want(U32(d, AssemblyOffset + 0x18) == 0xDEADBEEF, "Assembly sentinel is not 0xDEADBEEF");
            Want(I32(d, AssemblyOffset + 0x0C) == 144 && I32(d, AssemblyOffset + 0x10) == 144,
                 "Assembly self-size != 144");
            // +0x08 and Assembly Definition +0x00 are CONSTANTS, not pointers. They
            // coincide with the TOC offset on the 233 files whose TOC sits at 0x30,
            // and the 22 two-mesh files refute reading them as pointers: those put
            // the TOC at 0x40 and these stay 48/64.
            Want(I32(d, AssemblyOffset + 0x08) == 48, "Assembly +0x08 != 48");
            Want(I32(d, AssemblyOffset + 0x04) == DefinitionOffset,
                 "Assembly does not point at the Assembly Definition");
            Want(I32(d, AssemblyOffset) == BitTableOffset, "Assembly does not point at the BitTable");
            Want(I32(d, AssemblyOffset + 0x70) == Root.Offset,
                 "Assembly +0x70 does not point at the root volume");
            Want(I32(d, AssemblyOffset + 0x78) == DefinitionOffset + 0x44,
                 "Assembly +0x78 does not point at the inverse inertia");
            Want(I32(d, AssemblyOffset + 0x80) == DefinitionOffset + 0xB0,
                 "Assembly +0x80 does not point at Assembly Definition +0xb0");
            Want(I32(d, DefinitionOffset) == 64, "Assembly Definition +0x00 != 64");
            Vector3 mat = Vec3(d, DefinitionOffset + 0xB0);
            Want(Math.Abs(mat.X - 0.2f) < 1e-6 && Math.Abs(mat.Y - 0.2f) < 1e-6
                 && Math.Abs(mat.Z - 0.1f) < 1e-6, "Assembly Definition +0xb0 != (0.2, 0.2, 0.1)");
            Want(U32(d, DefinitionOffset + 0x54) == 0x749DC5AE, "Assembly Definition +0x54 != 1e32");
            Want(I32(d, DefinitionOffset + 0x40) == Root.Offset,
                 "Assembly Definition does not point at the root volume");
            Want(I32(d, DefinitionOffset + 0x28) == BitTableOffset,
                 "Assembly Definition does not point at the BitTable");
            Want(InverseInertia.X > 0 && InverseInertia.Y > 0 && InverseInertia.Z > 0,
                 FormattableString.Invariant(
                     $"inverse inertia diagonal is not positive: ({InverseInertia.X:G}, {InverseInertia.Y:G}, {InverseInertia.Z:G})"));

            // 100% byte accounting
            List<BlockSpan> spans = BlockMap();
            int cur = 0;
            int unexplained = 0;
            foreach (BlockSpan s in spans)
            {
                if (s.Start != cur)
                {
                    bad.Add($"block map gap/overlap at 0x{s.Start:x} (expected 0x{cur:x})");
                }
                if (s.Label == "UNEXPLAINED") { unexplained += s.End - s.Start; }
                cur = s.End;
            }
            if (cur != n) { bad.Add($"block map ends at 0x{cur:x}, file is 0x{n:x}"); }
            if (unexplained != 0) { bad.Add($"{unexplained} unexplained bytes"); }
            return bad;
        }

        // The declared array bbox must equal the bbox of the decoded contents to
        // within one quantisation step, BOTH ways. It is tight on purpose: a wrong
        // vertex decode, a wrong granularity and a wrong primitive reading all fail
        // it, and a loose one-sided test would catch none of them.
        private void CheckArrayBbox(List<string> bad)
        {
            var lo = new double[3] { double.MaxValue, double.MaxValue, double.MaxValue };
            var hi = new double[3] { double.MinValue, double.MinValue, double.MinValue };
            bool any = false;

            void Accumulate(double x, double y, double z)
            {
                any = true;
                double[] p = { x, y, z };
                for (int k = 0; k < 3; k++)
                {
                    lo[k] = Math.Min(lo[k], p[k]);
                    hi[k] = Math.Max(hi[k], p[k]);
                }
            }

            double tol = 1e-3;
            foreach (ClusteredMeshStruct m in Meshes)
            {
                tol = Math.Max(tol, m.Granularity);
            }

            foreach (VolumeStruct v in Volumes)
            {
                if (v.VolumeType == VolumeAggregate)
                {
                    ClusteredMeshStruct? m = MeshAt(v.Aggregate);
                    if (m == null) { continue; }
                    foreach (ClusterStruct c in m.Clusters)
                    {
                        foreach (Vector3 p in c.Vertices)
                        {
                            (double x, double y, double z) = TransformDouble(v, p.X, p.Y, p.Z);
                            Accumulate(x, y, z);
                        }
                    }
                }
                else if (v.VolumeType == VolumePrimitive2)
                {
                    // A cylinder about local Z, sampled at 16 points around each cap.
                    for (int k = 0; k < 16; k++)
                    {
                        double a = 2.0 * Math.PI * k / 16.0;
                        for (int s = 0; s < 2; s++)
                        {
                            double z = s == 0 ? -v.HalfHeight : v.HalfHeight;
                            (double x, double y, double zz) = TransformDouble(
                                v, v.Radius * Math.Cos(a), v.Radius * Math.Sin(a), z);
                            Accumulate(x, y, zz);
                        }
                    }
                }
            }

            if (!any) { return; }

            var min = new[] { (double)ArrayBboxMin.X, ArrayBboxMin.Y, ArrayBboxMin.Z };
            var max = new[] { (double)ArrayBboxMax.X, ArrayBboxMax.Y, ArrayBboxMax.Z };
            for (int k = 0; k < 3; k++)
            {
                if (!(Math.Abs(lo[k] - min[k]) <= tol && Math.Abs(hi[k] - max[k]) <= tol))
                {
                    bad.Add(FormattableString.Invariant(
                        $"array bbox axis {k} [{min[k]:F4}, {max[k]:F4}] != contents [{lo[k]:F4}, {hi[k]:F4}]"));
                }
            }
            if (!(min[0] <= max[0] && min[1] <= max[1] && min[2] <= max[2]))
            {
                bad.Add("array bbox min > max");
            }
        }

        private static void CheckMesh(List<string> bad, ClusteredMeshStruct m)
        {
            void Want(bool cond, string msg)
            {
                if (!cond) { bad.Add(msg); }
            }

            float g = m.Granularity;
            Want(g > 0, FormattableString.Invariant($"{m.Name} granularity {g:G}"));
            Want(m.KdTreeOffset == 0x60, $"{m.Name} kdtree offset 0x{m.KdTreeOffset:x}");
            Want(m.ClusterBits == BitLength(m.ClusterCount),
                 $"{m.Name} clusterBits {m.ClusterBits} != {BitLength(m.ClusterCount)}");
            Want(m.KdEntryCount == m.UnitCount,
                 $"{m.Name} KDTree entries {m.KdEntryCount} != unit count {m.UnitCount}");
            Want(m.ClusterCount == m.Clusters.Count, $"{m.Name} cluster count");
            if (m.KdBranchCount != 0)
            {
                Want(m.KdNodesOffset == 0xA0, $"{m.Name} branch nodes at 0x{m.KdNodesOffset:x}");
                Want(0xA0 + 32 * m.KdBranchCount == m.ClusterTableOffset,
                     $"{m.Name} branch nodes do not run up to the cluster table");
            }
            else
            {
                Want(m.KdNodesOffset == 0xFFFFFFFF && m.ClusterTableOffset == 0xA0,
                     $"{m.Name} empty KDTree is malformed");
            }
            Want(LessOrEqual(m.BboxMin, m.BboxMax), $"{m.Name} bbox min > max");
            for (int k = 0; k < 3; k++)
            {
                if (!(Component(m.KdBboxMin, k) == Component(m.BboxMin, k)
                      && Component(m.KdBboxMax, k) == Component(m.BboxMax, k)))
                {
                    bad.Add($"{m.Name} KDTree bbox != mesh bbox on axis {k}");
                }
            }

            int units = 0;
            int maxUnitDataSize = 0;
            for (int ci = 0; ci < m.Clusters.Count; ci++)
            {
                ClusterStruct c = m.Clusters[ci];
                maxUnitDataSize = Math.Max(maxUnitDataSize, c.UnitDataSize);
                Want(c.TotalSize == 16 + 16 * c.UnitDataStart + c.UnitDataSize,
                     $"{m.Name} cluster {ci} totalSize {c.TotalSize} != 16+{16 * c.UnitDataStart}+{c.UnitDataSize}");
                Want(Align16(c.VertexBytes) == 16 * c.UnitDataStart,
                     $"{m.Name} cluster {ci} vertex block {c.VertexBytes} does not fill {16 * c.UnitDataStart} bytes");
                Want(c.NormalCount == 0 && c.NormalStart == c.UnitDataStart,
                     $"{m.Name} cluster {ci} declares normals");
                Want(c.UnitBytes == c.UnitDataSize,
                     $"{m.Name} cluster {ci} unit stream is {c.UnitBytes} bytes, header says {c.UnitDataSize}");
                Want(c.UnitCount == c.Units.Count, $"{m.Name} cluster {ci} unit count");
                units += c.UnitCount;

                foreach (UnitStruct u in c.Units)
                {
                    int maxIdx = 0;
                    var distinct = new HashSet<byte>();
                    foreach (byte b in u.Indices)
                    {
                        maxIdx = Math.Max(maxIdx, b);
                        distinct.Add(b);
                    }
                    Want(maxIdx < c.VertexCount,
                         $"{m.Name} cluster {ci} index {maxIdx} >= {c.VertexCount} vertices");
                    Want(distinct.Count == u.Indices.Length,
                         $"{m.Name} cluster {ci} degenerate unit {FormatIndices(u.Indices)}");
                    Want(u.EdgeBytes.Length == u.Indices.Length,
                         $"{m.Name} cluster {ci} edge byte count");
                    Want(u.TypeByte == 0x21 || u.TypeByte == 0x22,
                         $"{m.Name} cluster {ci} unit type 0x{u.TypeByte:x2}");
                }

                // Every decoded vertex must sit inside the declared bbox, up to one
                // granularity. This is the assertion that fails first if the
                // quantisation or the cluster origin is read wrong.
                foreach (Vector3 p in c.Vertices)
                {
                    bool inside = true;
                    for (int k = 0; k < 3; k++)
                    {
                        float pk = Component(p, k);
                        if (!(Component(m.BboxMin, k) - g <= pk && pk <= Component(m.BboxMax, k) + g))
                        {
                            inside = false;
                        }
                    }
                    if (!inside)
                    {
                        bad.Add($"{m.Name} cluster {ci} vertex outside bbox+granularity");
                        break;
                    }
                }
            }
            Want(units == m.UnitCount, $"{m.Name} units {units} != declared {m.UnitCount}");
            if (m.Clusters.Count != 0)
            {
                int wantTagBits = BitLength(m.ClusterCount) + BitLength(maxUnitDataSize) + 1;
                Want(m.TagBits == wantTagBits,
                     $"{m.Name} tagBits {m.TagBits} does not match the cluster/unit bit budget");
            }

            if (m.KdBranchCount == 0)
            {
                return;
            }

            // KDTree: leaf count, unit coverage, parent links.
            List<LeafRef> leaves = m.LeafRefs();
            Want(leaves.Count == m.KdBranchCount + 1,
                 $"{m.Name} has {leaves.Count} KDTree leaves, expected {m.KdBranchCount + 1}");

            var cover = new Dictionary<(int, int), int>();
            long leafTotal = 0;
            foreach (LeafRef leaf in leaves)
            {
                leafTotal += leaf.UnitCount;
                if (leaf.ClusterIndex >= m.ClusterCount || leaf.ClusterIndex >= m.Clusters.Count)
                {
                    bad.Add($"{m.Name} leaf cluster {leaf.ClusterIndex} out of range");
                    continue;
                }
                ClusterStruct c = m.Clusters[leaf.ClusterIndex];
                int j = 0;
                if (leaf.UnitCount != 0)
                {
                    j = -1;
                    for (int i = 0; i < c.Units.Count; i++)
                    {
                        if (c.Units[i].ByteOffset == leaf.UnitByteOffset) { j = i; break; }
                    }
                    if (j < 0)
                    {
                        bad.Add($"{m.Name} leaf tag 0x{(leaf.ClusterIndex << 16) | leaf.UnitByteOffset:x} is not a unit boundary");
                        continue;
                    }
                }
                for (int i = 0; i < leaf.UnitCount; i++)
                {
                    var key = (leaf.ClusterIndex, j + i);
                    cover[key] = cover.TryGetValue(key, out int had) ? had + 1 : 1;
                }
            }

            bool partitions = true;
            int total = 0;
            for (int ci = 0; ci < m.Clusters.Count; ci++)
            {
                total += m.Clusters[ci].UnitCount;
                for (int j = 0; j < m.Clusters[ci].UnitCount; j++)
                {
                    if (!cover.TryGetValue((ci, j), out int c1) || c1 != 1) { partitions = false; }
                }
            }
            if (cover.Count != total) { partitions = false; }
            Want(partitions, $"{m.Name} KDTree leaves do not partition the units");
            Want(leafTotal == m.KdEntryCount, $"{m.Name} leaf counts do not sum to nEntries");

            var branch = new HashSet<uint>();
            for (int i = 0; i < m.BranchNodes.Count; i++)
            {
                BranchNodeStruct nd = m.BranchNodes[i];
                Want(nd.Axis >= 0 && nd.Axis <= 2, $"{m.Name} node {i} axis {nd.Axis}");
                Want(nd.Parent < m.KdBranchCount, $"{m.Name} node {i} parent out of range");
                for (int slot = 0; slot < 2; slot++)
                {
                    uint content = slot == 0 ? nd.Content0 : nd.Content1;
                    uint index = slot == 0 ? nd.Index0 : nd.Index1;
                    if (content != 0xFFFFFFFF) { continue; }
                    Want(index > 0 && index < (uint)m.KdBranchCount,
                         $"{m.Name} node {i} child index {index}");
                    if (index > 0 && index < (uint)m.KdBranchCount)
                    {
                        Want(m.BranchNodes[(int)index].Parent == i,
                             $"{m.Name} node {index} parent link broken");
                        branch.Add(index);
                    }
                }
            }
            bool singleTree = branch.Count == m.KdBranchCount - 1;
            for (uint i = 1; i < (uint)m.KdBranchCount && singleTree; i++)
            {
                if (!branch.Contains(i)) { singleTree = false; }
            }
            Want(singleTree, $"{m.Name} branch nodes are not a single tree");
        }

        // Corpus table. The tens digit is the material family (0 none, 1 rock,
        // 2 ice, 3 wood, 4 metal, 5 concrete); the ones digit varies within a
        // family and its meaning is UNKNOWN -- train_rail_a..g use wood 30 for
        // the sleepers where train_rail_jump/straight use wood 31 for the same
        // part. Not decoded, deliberately.
        private static bool IsKnownSurfaceId(int id)
        {
            switch (id)
            {
                case 0:
                case 10:
                case 11:
                case 21:
                case 30:
                case 31:
                case 40:
                case 41:
                case 50:
                case 51:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Material family for a Volume's surfaceID, or null for an id outside the
        /// corpus table. Only the tens digit is understood.
        /// </summary>
        public static string? SurfaceFamily(int surfaceId)
        {
            switch (surfaceId / 10)
            {
                case 0: return "none";
                case 1: return "rock";
                case 2: return "ice";
                case 3: return "wood";
                case 4: return "metal";
                case 5: return "concrete";
                default: return null;
            }
        }

        private static string? ClassOfTypeId(int typeId)
        {
            switch (typeId)
            {
                case 1: return "SimpleMappedArray";
                case 2: return "rwcAggregateNode1";
                case 3: return "ClusteredMesh";
                case 4: return "Assembly BitTable";
                case 5: return "Assembly Definition";
                case 6: return "Assembly";
                default: return null;
            }
        }

        // Types 1 and 3 name their object with a prefix ("rwcMeshNode0_ClusteredMesh"),
        // so those match by substring; the rest are exact.
        private static bool IsExactNameType(int typeId) =>
            typeId == 2 || typeId == 4 || typeId == 5 || typeId == 6;

        // -- OBJ export -------------------------------------------------------

        /// <summary>
        /// The collision triangles as a flat OBJ -- three unshared vertices per
        /// face, in decode order. Primitive volumes have no triangles of their own
        /// and are emitted as trailing comments rather than tessellated, so the
        /// mesh is only what the file actually stores.
        /// </summary>
        public void ExportObj(string path)
        {
            List<Triangle> tris = Tris();
            var output = new StringBuilder();
            output.Append("# ").Append(Path.GetFileName(SourcePath))
                  .Append(" -- SSX 2012 .pob collision proxy\n");

            foreach (Triangle t in tris)
            {
                AppendVertex(output, t.A);
                AppendVertex(output, t.B);
                AppendVertex(output, t.C);
            }
            for (int i = 0; i < tris.Count; i++)
            {
                output.Append("f ").Append(3 * i + 1).Append(' ')
                      .Append(3 * i + 2).Append(' ').Append(3 * i + 3).Append('\n');
            }
            for (int i = 0; i < Volumes.Count; i++)
            {
                VolumeStruct v = Volumes[i];
                if (v.VolumeType != VolumePrimitive2) { continue; }
                Vector3 t = v.Translation;
                output.Append(FormattableString.Invariant(
                    $"# volume {i}: primitive halfHeight={v.HalfHeight:F4} radius={v.Radius:F4} axis=localZ at ({t.X:F4}, {t.Y:F4}, {t.Z:F4})\n"));
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.WriteAllText(path, output.ToString());
        }

        // "F6", not the custom "0.000000": custom numeric formats round a float
        // through a 7-significant-digit intermediate first, so the last digit can
        // differ from the true rounding. The error is invisible at small magnitudes
        // and grows with them, which is exactly the wrong way round for a spot check.
        private static void AppendVertex(StringBuilder output, Vector3 p)
        {
            output.Append("v ")
                  .Append(p.X.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(p.Y.ToString("F6", CultureInfo.InvariantCulture)).Append(' ')
                  .Append(p.Z.ToString("F6", CultureInfo.InvariantCulture)).Append('\n');
        }

        // -- primitive reads --------------------------------------------------

        // Explicit little-endian rather than BitConverter, which is host-order:
        // correct on x64 and ARM64 today, silently wrong on a big-endian host.
        //private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o, 4));

        //private static int I32(byte[] d, int o) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o, 4));

        //private static int U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o, 2));

        //private static float F32(byte[] d, int o) => BitConverter.UInt32BitsToSingle(U32(d, o));

        //private static Vector3 Vec3(byte[] d, int o) => new Vector3(F32(d, o), F32(d, o + 4), F32(d, o + 8));

        //private static string CString(byte[] d, int o)
        //{
        //    int e = o;
        //    while (e < d.Length && d[e] != 0) { e++; }
        //    return Encoding.ASCII.GetString(d, o, e - o);
        //}

        //// -- small helpers ----------------------------------------------------

        //private static int Align16(int x) => (x + 15) / 16 * 16;

        // Python's int.bit_length(): 0 for 0, else the position of the high bit.
        private static int BitLength(int x)
        {
            int n = 0;
            while (x > 0) { n++; x >>= 1; }
            return n;
        }

        private static float Component(Vector3 v, int k) => k == 0 ? v.X : k == 1 ? v.Y : v.Z;

        private static bool LessOrEqual(Vector3 a, Vector3 b) =>
            a.X <= b.X && a.Y <= b.Y && a.Z <= b.Z;

        private static bool SameInts(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) { return false; }
            }
            return true;
        }

        private static string FormatIndices(byte[] idx)
        {
            var sb = new StringBuilder("(");
            for (int i = 0; i < idx.Length; i++)
            {
                if (i != 0) { sb.Append(", "); }
                sb.Append(idx[i]);
            }
            return sb.Append(')').ToString();
        }

        // -- structs ------------------------------------------------------------

        public struct TypeDescStruct
        {
            public int TypeId;
            public int Count;
            public int Offset;   // relative to the TOC

            public override string ToString() => $"type {TypeId} x{Count}";
        }

        public struct ObjectEntryStruct
        {
            public int Offset;   // absolute file offset
            public string Name;

            public override string ToString() => $"{Name}@0x{Offset:x}";
        }

        /// <summary>
        /// A 96-byte rwc Volume: a transform plus either an aggregate pointer or a
        /// two-parameter primitive.
        /// </summary>
        public class VolumeStruct
        {
            public const int Size = 0x60;

            public int Offset;

            /// <summary>Matrix44Affine: three basis rows then the translation.</summary>
            public Vector3[] Rows = new Vector3[4];

            public int VolumeType;

            /// <summary>Absolute file offset of the aggregate, for volumeType 6.</summary>
            public int Aggregate;

            /// <summary>Half height along local Z, for volumeType 5.</summary>
            public float HalfHeight;

            /// <summary>Radius, for volumeType 5.</summary>
            public float Radius;

            /// <summary>Edge radius. 0.0 in all 278 volumes surveyed.</summary>
            public float Fatness;

            /// <summary>
            /// Collision filter group. 11 is exclusively the 19 standing tree props,
            /// so it is a real group; 0/1/2 are a per-shape discriminator of unknown
            /// meaning (the two volumes of a two-mesh prop always differ).
            /// </summary>
            public int GroupId;

            /// <summary>Material class. See <see cref="SurfaceFamily"/>.</summary>
            public int SurfaceId;

            /// <summary>1 in 276 of the 278 volumes surveyed, 0 in the other two.</summary>
            public int Flags;

            public Vector3 Translation => Rows[3];

            public bool IsAggregate => VolumeType == VolumeAggregate;

            public bool IsPrimitive => VolumeType == VolumePrimitive2;

            public override string ToString()
            {
                string what = VolumeType == VolumeAggregate
                    ? FormattableString.Invariant($"aggregate@0x{Aggregate:x}")
                    : VolumeType == VolumePrimitive2
                        ? FormattableString.Invariant($"primitive halfHeight={HalfHeight:F4} radius={Radius:F4}")
                        : FormattableString.Invariant($"volumeType {VolumeType}");
                return FormattableString.Invariant(
                    $"<Volume {what} group={GroupId} surface={SurfaceId}({SurfaceFamily(SurfaceId) ?? "?"}) flags={Flags} at ({Translation.X:F3}, {Translation.Y:F3}, {Translation.Z:F3})>");
            }
        }

        /// <summary>
        /// One ClusteredMesh cluster: up to 255 shared quantised vertices and a
        /// packed, variable-stride unit stream.
        /// </summary>
        public class ClusterStruct
        {
            public int Offset;

            public int UnitCount;
            public int UnitDataSize;

            /// <summary>In 16-byte units, measured from the end of the 16-byte header.</summary>
            public int UnitDataStart;

            /// <summary>Same base as <see cref="UnitDataStart"/>. Equal to it everywhere, since no cluster carries normals.</summary>
            public int NormalStart;

            public int TotalSize;
            public int VertexCount;
            public int NormalCount;   // 0 everywhere on the disc

            /// <summary>1 == 16-bit, the only mode on the disc.</summary>
            public int Compression;

            /// <summary>Cluster origin, in granularity units.</summary>
            public QuantisedVertex Origin;

            /// <summary>Vertices as stored: integer multiples of the granularity.</summary>
            public List<QuantisedVertex> QuantisedVertices = new List<QuantisedVertex>();

            /// <summary><see cref="QuantisedVertices"/> scaled by the granularity, in cluster-local metres.</summary>
            public List<Vector3> Vertices = new List<Vector3>();

            public List<UnitStruct> Units = new List<UnitStruct>();

            public int VertexBytes;
            public int UnitBytes;
            public int UnitOffset;

            /// <summary>
            /// Triangles, as cluster-local vertex triples. Quads split (0,1,2) +
            /// (1,3,2), which is verified against the render meshes.
            /// </summary>
            public List<Triangle> Tris()
            {
                var output = new List<Triangle>();
                foreach (UnitStruct u in Units)
                {
                    byte[] i = u.Indices;
                    output.Add(new Triangle { A = Vertices[i[0]], B = Vertices[i[1]], C = Vertices[i[2]] });
                    if (i.Length == 4)
                    {
                        output.Add(new Triangle { A = Vertices[i[1]], B = Vertices[i[3]], C = Vertices[i[2]] });
                    }
                }
                return output;
            }

            public override string ToString() => $"<Cluster @0x{Offset:x} {VertexCount}v {UnitCount}u>";
        }

        /// <summary>The triangle/quad soup and KD-tree that is 99% of a `.pob`.</summary>
        public class ClusteredMeshStruct
        {
            public int Offset;
            public string Name = "";

            public Vector3 BboxMin;
            public Vector3 BboxMax;

            /// <summary>
            /// Bits needed for a unit tag. Surveyed as clusterBits +
            /// bitlength(max unitDataSize) + 1 on 273/273 meshes.
            /// </summary>
            public int TagBits;

            public int UnitCount;
            public int KdTreeOffset;        // 0x60 everywhere; relative to the object
            public int ClusterTableOffset;  // relative to the object

            /// <summary>Vertex compression granularity. 0.04 in all 273 meshes surveyed.</summary>
            public float Granularity;

            public int ClusterFlags;   // 0x0010, constant; meaning unknown
            public int GroupIdSize;    // 0x00, constant
            public int SurfaceIdSize;  // 0x02, constant

            public int ClusterCount;
            public int Size;           // this object's size in bytes
            public int U58;            // 0x80, constant; meaning unknown
            public int ClusterBits;

            /// <summary>0xFFFFFFFF when the tree is empty, else 0xA0.</summary>
            public uint KdNodesOffset;

            public int KdBranchCount;
            public int KdEntryCount;    // == UnitCount
            public Vector3 KdBboxMin;
            public Vector3 KdBboxMax;

            public List<BranchNodeStruct> BranchNodes = new List<BranchNodeStruct>();

            /// <summary>Absolute file offset of the cluster offset table.</summary>
            public int ClusterTable;

            /// <summary>Cluster offsets, relative to <see cref="ClusterTable"/>.</summary>
            public List<int> ClusterOffsets = new List<int>();

            public List<ClusterStruct> Clusters = new List<ClusterStruct>();

            /// <summary>Every cluster's triangles, in cluster order.</summary>
            public List<Triangle> Tris()
            {
                var output = new List<Triangle>();
                foreach (ClusterStruct c in Clusters)
                {
                    output.AddRange(c.Tris());
                }
                return output;
            }

            /// <summary>Every cluster's vertices, in cluster order.</summary>
            public List<Vector3> Verts()
            {
                var output = new List<Vector3>();
                foreach (ClusterStruct c in Clusters)
                {
                    output.AddRange(c.Vertices);
                }
                return output;
            }

            /// <summary>
            /// The KD-tree's leaves, in (node, slot) order. Leaves number
            /// branchNodes+1, their counts sum to nEntries, and their unit ranges
            /// partition the unit set exactly on 267/267 non-empty trees surveyed.
            /// </summary>
            public List<LeafRef> LeafRefs()
            {
                var output = new List<LeafRef>();
                for (int i = 0; i < BranchNodes.Count; i++)
                {
                    BranchNodeStruct n = BranchNodes[i];
                    for (int slot = 0; slot < 2; slot++)
                    {
                        uint content = slot == 0 ? n.Content0 : n.Content1;
                        uint index = slot == 0 ? n.Index0 : n.Index1;
                        if (content == 0xFFFFFFFF) { continue; }
                        output.Add(new LeafRef
                        {
                            BranchIndex = i,
                            Slot = slot,
                            UnitCount = (int)content,
                            ClusterIndex = (int)(index >> 16),
                            UnitByteOffset = (int)(index & 0xFFFF),
                        });
                    }
                }
                return output;
            }

            public override string ToString() =>
                $"<ClusteredMesh {Name} {ClusterCount}c {UnitCount}u>";
        }

        /// <summary>
        /// A 32-byte KD-tree branch node. Each child is a NodeRef of (content,
        /// index): content 0xFFFFFFFF means `index` is another branch node,
        /// otherwise `content` is a unit COUNT and `index` is a unit tag,
        /// `clusterIndex &lt;&lt; 16 | byteOffsetIntoThatClustersUnitData`.
        /// </summary>
        public struct BranchNodeStruct
        {
            public int Parent;
            public int Axis;      // 0, 1 or 2
            public uint Content0;
            public uint Index0;
            public uint Content1;
            public uint Index1;
            public float Extent0;
            public float Extent1;
        }

        /// <summary>A resolved KD-tree leaf. See <see cref="ClusteredMeshStruct.LeafRefs"/>.</summary>
        public struct LeafRef
        {
            public int BranchIndex;
            public int Slot;
            public int UnitCount;
            public int ClusterIndex;
            public int UnitByteOffset;
        }

        /// <summary>
        /// One polygon. The type byte's low nibble gives the side count (1 =
        /// triangle, 2 = quad); bit 0x20, set on every unit on the disc, says a
        /// per-edge byte array follows the indices. Only 0x21 and 0x22 occur.
        /// </summary>
        public struct UnitStruct
        {
            /// <summary>Offset into this cluster's unit data. KD-tree tags address units by it.</summary>
            public int ByteOffset;

            public byte TypeByte;

            /// <summary>Indices into the owning cluster's vertex array.</summary>
            public byte[] Indices;

            /// <summary>
            /// One byte per edge, edge i spanning (index[i], index[i+1 mod n]).
            /// The low 5 bits are a log-quantised dihedral angle across that edge;
            /// bits 0x20 and 0x80 are UNEXPLAINED. Only 55 distinct values occur in
            /// 288341 edges. Exposed raw rather than decoded.
            /// </summary>
            public byte[] EdgeBytes;

            public bool IsQuad => (TypeByte & 0x0F) == 2;
        }

        /// <summary>A cluster vertex as stored: an integer multiple of the granularity.</summary>
        public struct QuantisedVertex
        {
            public int X;
            public int Y;
            public int Z;

            public override string ToString() => $"({X}, {Y}, {Z})";
        }

        public struct Triangle
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
        }

        /// <summary>A labelled byte range from <see cref="BlockMap"/>.</summary>
        public struct BlockSpan
        {
            public int Start;
            public int End;
            public string Label;

            public BlockSpan(int start, int end, string label)
            {
                Start = start;
                End = end;
                Label = label;
            }

            public int Length => End - Start;

            public override string ToString() => $"0x{Start:x6} .. 0x{End:x6}  {Label}";
        }
    }
}
