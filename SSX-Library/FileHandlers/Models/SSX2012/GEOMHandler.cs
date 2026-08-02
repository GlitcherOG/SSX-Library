using SSX_Library.Internal.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace SSXLibrary.FileHandlers.Models.SSX2012
{
    // VFMT-driven `.geom` reader. The vertex layout is NOT fixed -- it is
    // described by the VFMT chunk (and split across the file's STRM chunks),
    // so reading a hardcoded 32-byte "position+normal+tangent+uv" stride
    // (the old approach) only works for character models and walks off the
    // end of every track proxy, which uses a shorter, different layout.
    //
    // This is a straight port of `ssx/ssx2012_geom.py`, which is the verified
    // reference implementation (218 `.geom` files off the retail disc) -- see
    // that file's module docstring for the full format writeup. Keep this
    // reader's behaviour in lockstep with it; if they ever disagree, the
    // Python side is the ground truth until proven otherwise.
    public class GEOMHandler
    {
        public string Magic;
        public int U0;
        public int U1;
        public int U2;

        public GEOMStruct geomStruct = new GEOMStruct();

        // One VFMT describes the combined vertex layout; if the vertex is
        // split over more than one stream, one further VFMT follows per
        // stream. `attributes` is parsed from the first (combined) string.
        public List<string> vfmtStrings = new List<string>();
        public List<VfmtAttribute> attributes = new List<VfmtAttribute>();

        public List<STRMStruct> streams = new List<STRMStruct>();
        public List<INDXStruct> indexBuffers = new List<INDXStruct>();

        // Every file surveyed carries exactly one MESH chunk; if that ever
        // stops being true, the loop in Load() still reads every one of them,
        // it just leaves only the last one here.
        public MESHStruct meshStruct = new MESHStruct();

        public List<Vertex> vertices = new List<Vertex>();
        public List<Faces> faces = new List<Faces>();

        public void Load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                Magic = StreamUtil.ReadString(stream, 4);
                U0 = StreamUtil.ReadUInt32(stream, true);
                U1 = StreamUtil.ReadUInt32(stream, true);
                U2 = StreamUtil.ReadUInt32(stream, true);

                geomStruct = new GEOMStruct
                {
                    Magic = ReadTag(stream, "GEOM"),
                    U0 = StreamUtil.ReadUInt32(stream, true),
                    GeomSize = StreamUtil.ReadUInt32(stream, true),
                    U2 = StreamUtil.ReadUInt32(stream, true),
                    U3 = StreamUtil.ReadUInt32(stream, true),
                };

                // Read VFMT chunks greedily -- the stream count that would
                // tell us how many to expect comes AFTER them.
                vfmtStrings = new List<string>();
                while (PeekTagIs(stream, "VFMT"))
                {
                    ReadTag(stream, "VFMT");
                    int len = StreamUtil.ReadUInt32(stream, true);
                    vfmtStrings.Add(StreamUtil.ReadString(stream, len + 1));
                }
                attributes = ParseVfmt(vfmtStrings[0]);

                int streamCount = StreamUtil.ReadUInt32(stream, true);
                streams = new List<STRMStruct>(streamCount);
                for (int i = 0; i < streamCount; i++)
                {
                    streams.Add(ReadStream(stream));
                }
                ValidateStreams();

                int indexBufferCount = StreamUtil.ReadUInt32(stream, true);
                indexBuffers = new List<INDXStruct>(indexBufferCount);
                for (int i = 0; i < indexBufferCount; i++)
                {
                    indexBuffers.Add(ReadIndx(stream));
                }

                StreamUtil.ReadUInt32(stream, true); // unknown, always 0 on disc

                int meshCount = StreamUtil.ReadUInt32(stream, true);
                for (int i = 0; i < meshCount; i++)
                {
                    meshStruct = ReadMesh(stream, streamCount);
                }
            }

            BuildVertices();
            GenerateFaces();
        }

        // -- chunk readers -----------------------------------------------

        private static string ReadTag(Stream stream, string expect = null)
        {
            string tag = StreamUtil.ReadString(stream, 4);
            if (expect != null && tag != expect)
            {
                throw new InvalidDataException($"expected '{expect}' chunk tag, got '{tag}'");
            }
            return tag;
        }

        private static bool PeekTagIs(Stream stream, string expect)
        {
            string tag = StreamUtil.ReadString(stream, 4);
            stream.Position -= 4;
            return tag == expect;
        }

        // A vertex's attributes can live in different streams -- `n0` can be
        // offset 0 of stream *1*, not of stream 0 -- so each STRM's raw bytes
        // are kept as-is and only combined into a `Vertex` once every stream
        // has been read (see BuildVertices).
        private static STRMStruct ReadStream(Stream stream)
        {
            var strm = new STRMStruct
            {
                STRMMagic = ReadTag(stream, "STRM"),
                U0 = StreamUtil.ReadInt8(stream),
                U1 = StreamUtil.ReadUInt32(stream, true),
                NumVertices = StreamUtil.ReadUInt32(stream, true),
                Stride = StreamUtil.ReadUInt32(stream, true),
                U3 = StreamUtil.ReadUInt32(stream, true),
            };

            int byteCount = strm.NumVertices * strm.Stride;
            strm.Data = StreamUtil.ReadBytes(stream, byteCount);
            if (strm.Data.Length < byteCount)
            {
                throw new InvalidDataException("truncated vertex stream");
            }
            return strm;
        }

        private static INDXStruct ReadIndx(Stream stream)
        {
            var indx = new INDXStruct
            {
                INDXMagic = ReadTag(stream, "INDX"),
                U0 = StreamUtil.ReadUInt8(stream),
                U1 = StreamUtil.ReadUInt32(stream, true),
                IndexCount = StreamUtil.ReadUInt32(stream, true),
                IndexMode = StreamUtil.ReadString(stream, 4),
            };

            indx.Indexs = new List<int>(indx.IndexCount);
            switch (indx.IndexMode)
            {
                case "ID16":
                    for (int i = 0; i < indx.IndexCount; i++)
                    {
                        indx.Indexs.Add(StreamUtil.ReadUInt16(stream, true));
                    }
                    break;
                case "ID32":
                    for (int i = 0; i < indx.IndexCount; i++)
                    {
                        indx.Indexs.Add(StreamUtil.ReadInt32(stream, true));
                    }
                    break;
                default:
                    throw new InvalidDataException($"unknown INDX mode '{indx.IndexMode}'");
            }
            return indx;
        }

        private static MESHStruct ReadMesh(Stream stream, int nstreams)
        {
            var mesh = new MESHStruct { Groups = new List<MeshGroup>() };

            ReadTag(stream, "MESH");
            StreamUtil.ReadUInt32(stream, true); // unknown

            ReadTag(stream, "TLST");
            int[] tlst = new int[5];
            for (int i = 0; i < tlst.Length; i++)
            {
                tlst[i] = StreamUtil.ReadUInt32(stream, true);
            }
            mesh.TriangleCount = tlst[2];

            // TLST re-states the stream count and then lists that many stream
            // ids -- that's why its payload is 10 u32 on a one-stream mesh and
            // 11 on a two-stream one. Cross-check it against the STRM list.
            int tlstStreamCount = StreamUtil.ReadUInt32(stream, true);
            if (tlstStreamCount != nstreams)
            {
                throw new InvalidDataException(
                    $"TLST says {tlstStreamCount} streams, file has {nstreams}");
            }
            for (int i = 0; i < tlstStreamCount; i++)
            {
                StreamUtil.ReadUInt32(stream, true);
            }
            StreamUtil.ReadUInt32(stream, true);
            StreamUtil.ReadUInt32(stream, true);
            StreamUtil.ReadUInt32(stream, true);

            ReadTag(stream, "GEOM");
            mesh.Version = StreamUtil.ReadString(stream, 4); // "GEO3" or "GEO4"

            int nameLen = StreamUtil.ReadUInt32(stream, true);
            mesh.Name = StreamUtil.ReadString(stream, nameLen + 1);

            mesh.BboxMin = StreamUtil.ReadVector3(stream, true);
            mesh.BboxMax = StreamUtil.ReadVector3(stream, true);

            if (mesh.Version != "GEO4")
            {
                // GEO3 (characters) stops here: one mesh, no spatial groups.
                return mesh;
            }

            StreamUtil.ReadUInt32(stream, true);
            StreamUtil.ReadUInt32(stream, true);
            int groupCount = StreamUtil.ReadUInt8(stream);

            var boxes = new (Vector3 Min, Vector3 Max)[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                boxes[i] = (StreamUtil.ReadVector3(stream, true), StreamUtil.ReadVector3(stream, true));
            }
            foreach (var box in boxes)
            {
                mesh.Groups.Add(new MeshGroup
                {
                    BboxMin = box.Min,
                    BboxMax = box.Max,
                    FirstTriangle = StreamUtil.ReadUInt32(stream, true),
                    TriangleCount = StreamUtil.ReadUInt32(stream, true),
                });
            }

            // Fixed trailer (1, 0, 0xFFFFFFFF), then zero padding to a
            // 16-byte file size -- nothing further to parse after it.
            StreamUtil.ReadUInt32(stream, true);
            StreamUtil.ReadUInt32(stream, true);
            StreamUtil.ReadUInt32(stream, true);

            return mesh;
        }

        // -- VFMT -----------------------------------------------------------

        // type -> byte width. Anything not in here is a hard error: a
        // silently-skipped attribute would shift every later attribute's offset.
        private static readonly Dictionary<string, int> VfmtTypeWidths = new Dictionary<string, int>
        {
            { "4f32", 16 },
            { "3f32", 12 },
            { "2f32", 8 },
            { "3s10n", 4 },
            { "4u8n", 4 },
            { "4f16", 8 },
            { "2f16", 4 },
        };

        // `name:hexOffset:stream:????:type`, e.g. `n0:00:01:0001:3s10n` is
        // offset 0 of stream *1*. Field 4 is always `0001` on disc -- there is
        // no evidence to decode it, so it is parsed but ignored.
        public static List<VfmtAttribute> ParseVfmt(string text)
        {
            var attrs = new List<VfmtAttribute>();
            foreach (string token in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(':');
                if (parts.Length != 5)
                {
                    throw new FormatException($"bad VFMT entry '{token}'");
                }
                if (!VfmtTypeWidths.TryGetValue(parts[4], out int width))
                {
                    throw new NotSupportedException($"unknown VFMT type '{parts[4]}' in '{token}'");
                }

                attrs.Add(new VfmtAttribute
                {
                    Name = parts[0],
                    Kind = parts[0][0],
                    Index = parts[0].Length > 1 ? int.Parse(parts[0].Substring(1), CultureInfo.InvariantCulture) : 0,
                    Offset = Convert.ToInt32(parts[1], 16),
                    Stream = Convert.ToInt32(parts[2], 16),
                    Type = parts[4],
                    Width = width,
                });
            }
            return attrs;
        }

        private void ValidateStreams()
        {
            if (streams.Count == 0)
            {
                throw new InvalidDataException("GEOM file declares zero vertex streams");
            }
            int nverts = streams[0].NumVertices;
            foreach (var s in streams)
            {
                if (s.NumVertices != nverts)
                {
                    throw new InvalidDataException(
                        $"streams disagree on vertex count ({s.NumVertices} vs {nverts})");
                }
            }
            foreach (var attr in attributes)
            {
                if (attr.Stream >= streams.Count)
                {
                    throw new InvalidDataException(
                        $"'{attr.Name}' names stream {attr.Stream}, file has {streams.Count}");
                }
                if (attr.Offset + attr.Width > streams[attr.Stream].Stride)
                {
                    throw new InvalidDataException(
                        $"'{attr.Name}' overruns stream {attr.Stream}'s stride {streams[attr.Stream].Stride}");
                }
            }
        }

        // Decodes one attribute of one vertex out of its stream's raw bytes.
        // `3s10n` reuses the existing (and already-correct) StreamUtil bit
        // trick: three signed 10-bit fields packed into one big-endian u32,
        // X in the low bits, value/512.
        private static float[] DecodeAttribute(VfmtAttribute attr, byte[] data, int vertexBase)
        {
            int offset = vertexBase + attr.Offset;
            using (var ms = new MemoryStream(data, offset, attr.Width, writable: false))
            {
                switch (attr.Type)
                {
                    case "4f32":
                        return new[]
                        {
                            StreamUtil.ReadFloat(ms, true), StreamUtil.ReadFloat(ms, true),
                            StreamUtil.ReadFloat(ms, true), StreamUtil.ReadFloat(ms, true),
                        };
                    case "3f32":
                        return new[]
                        {
                            StreamUtil.ReadFloat(ms, true), StreamUtil.ReadFloat(ms, true),
                            StreamUtil.ReadFloat(ms, true),
                        };
                    case "2f32":
                        return new[] { StreamUtil.ReadFloat(ms, true), StreamUtil.ReadFloat(ms, true) };
                    case "4f16":
                        return new[]
                        {
                            StreamUtil.ReadHalfFloat(ms, true), StreamUtil.ReadHalfFloat(ms, true),
                            StreamUtil.ReadHalfFloat(ms, true), StreamUtil.ReadHalfFloat(ms, true),
                        };
                    case "2f16":
                        return new[] { StreamUtil.ReadHalfFloat(ms, true), StreamUtil.ReadHalfFloat(ms, true) };
                    case "4u8n":
                        return new[]
                        {
                            StreamUtil.ReadUInt8(ms) / 255f, StreamUtil.ReadUInt8(ms) / 255f,
                            StreamUtil.ReadUInt8(ms) / 255f, StreamUtil.ReadUInt8(ms) / 255f,
                        };
                    case "3s10n":
                        {
                            float x = StreamUtil.ReadIntCustom(ms, 4, 10, 0, true) / 512f;
                            ms.Position -= 4;
                            float y = StreamUtil.ReadIntCustom(ms, 4, 10, 10, true) / 512f;
                            ms.Position -= 4;
                            float z = StreamUtil.ReadIntCustom(ms, 4, 10, 20, true) / 512f;
                            return new[] { x, y, z };
                        }
                    default:
                        throw new NotSupportedException($"unknown VFMT type '{attr.Type}'");
                }
            }
        }

        // -- vertex / face assembly -----------------------------------------

        // Only the first attribute of each semantic kind (p/n/g/c/t) feeds the
        // typed Vertex -- a second UV or colour set, if a file ever has one,
        // is parsed but not exposed here, same as the Python reference's OBJ
        // export.
        private void BuildVertices()
        {
            vertices = new List<Vertex>();
            if (streams.Count == 0)
            {
                return;
            }

            var posAttr = attributes.FirstOrDefault(a => a.Kind == 'p');
            var normAttr = attributes.FirstOrDefault(a => a.Kind == 'n');
            var tanAttr = attributes.FirstOrDefault(a => a.Kind == 'g');
            var colAttr = attributes.FirstOrDefault(a => a.Kind == 'c');
            var uvAttr = attributes.FirstOrDefault(a => a.Kind == 't');

            int nverts = streams[0].NumVertices;
            for (int i = 0; i < nverts; i++)
            {
                var vert = new Vertex();
                if (posAttr != null)
                {
                    float[] v = ReadAttr(posAttr, i);
                    vert.Position = new Vector3(v[0], v[1], v[2]);
                }
                if (normAttr != null)
                {
                    float[] v = ReadAttr(normAttr, i);
                    vert.Normal = new Vector3(v[0], v[1], v[2]);
                }
                if (tanAttr != null)
                {
                    float[] v = ReadAttr(tanAttr, i);
                    vert.Tangent = new Vector3(v[0], v[1], v[2]);
                }
                if (colAttr != null)
                {
                    float[] v = ReadAttr(colAttr, i);
                    vert.Color = new Vector4(v[0], v[1], v[2], v[3]);
                }
                if (uvAttr != null)
                {
                    float[] v = ReadAttr(uvAttr, i);
                    vert.UV = new Vector2(v[0], v[1]);
                }
                vertices.Add(vert);
            }
        }

        private float[] ReadAttr(VfmtAttribute attr, int vertexIndex)
        {
            STRMStruct strm = streams[attr.Stream];
            int vertexBase = vertexIndex * strm.Stride;
            return DecodeAttribute(attr, strm.Data, vertexBase);
        }

        public void GenerateFaces()
        {
            faces = new List<Faces>();
            List<int> indices = indexBuffers.Count > 0 ? indexBuffers[0].Indexs : new List<int>();

            for (int i = 0; i < indices.Count / 3; i++)
            {
                faces.Add(new Faces
                {
                    V1 = vertices[indices[i * 3]],
                    V2 = vertices[indices[i * 3 + 1]],
                    V3 = vertices[indices[i * 3 + 2]],
                });
            }
        }

        // OBJ export. Vertices/UVs are deduplicated with dictionaries instead
        // of `List.Contains`/`IndexOf` -- that was O(n^2) over the face list
        // and took minutes on a 20k-vert track proxy. Output format (header
        // comment, section order, `o Mesh 0` placed after the geometry, v/vt
        // triple in the face lines) is unchanged from before.
        public void ExportModels(string path)
        {
            var faceLines = new StringBuilder();
            var output = new StringBuilder("# Exported From SSX Using SSX Multitool Modder by GlitcherOG \n");

            var vertexList = new List<Vector3>();
            var vertexIndex = new Dictionary<Vector3, int>();
            var uvList = new List<Vector2>();
            var uvIndex = new Dictionary<Vector2, int>();
            var normalList = new List<Vector3>();
            var normalIndex = new Dictionary<Vector3, int>();

            faceLines.Append("o Mesh 0\n");

            foreach (var face in faces)
            {
                int vPos1 = IndexOfOrAdd(face.V1.Position, vertexList, vertexIndex);
                int vPos2 = IndexOfOrAdd(face.V2.Position, vertexList, vertexIndex);
                int vPos3 = IndexOfOrAdd(face.V3.Position, vertexList, vertexIndex);

                int uPos1 = IndexOfOrAdd(face.V1.UV, uvList, uvIndex);
                int uPos2 = IndexOfOrAdd(face.V2.UV, uvList, uvIndex);
                int uPos3 = IndexOfOrAdd(face.V3.UV, uvList, uvIndex);

                int nPos1 = IndexOfOrAdd(face.V1.Normal, normalList, normalIndex);
                int nPos2 = IndexOfOrAdd(face.V2.Normal, normalList, normalIndex);
                int nPos3 = IndexOfOrAdd(face.V3.Normal, normalList, normalIndex);

                faceLines.Append("f ")
                    .Append(vPos1).Append('/').Append(uPos1).Append('/').Append(nPos1).Append(' ')
                    .Append(vPos2).Append('/').Append(uPos2).Append('/').Append(nPos2).Append(' ')
                    .Append(vPos3).Append('/').Append(uPos3).Append('/').Append(nPos3).Append('\n');
            }

            foreach (var v in vertexList)
            {
                output.Append("v ")
                    .Append(v.X.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(v.Y.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(v.Z.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            foreach (var uv in uvList)
            {
                output.Append("vt ")
                    .Append(uv.X.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append((-uv.Y).ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            // Normals are folded into the dedup above (to keep it O(n)) but,
            // same as before, never written as `vn` lines -- only their index
            // shows up in the face lines.
            output.Append(faceLines);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.WriteAllText(path, output.ToString());
        }

        private static int IndexOfOrAdd<T>(T value, List<T> list, Dictionary<T, int> index)
        {
            if (!index.TryGetValue(value, out int i))
            {
                i = list.Count;
                list.Add(value);
                index[value] = i;
            }
            return i + 1; // OBJ indices are 1-based
        }

        // -- structs ----------------------------------------------------------

        public struct GEOMStruct
        {
            public string Magic;
            public int U0;
            public int GeomSize;
            public int U2;
            public int U3;
        }

        public class VfmtAttribute
        {
            public string Name;
            public char Kind;
            public int Index;
            public int Offset;
            public int Stream;
            public string Type;
            public int Width;

            public override string ToString() => $"{Name}@s{Stream}+0x{Offset:X2}:{Type}";
        }

        public struct STRMStruct
        {
            public string STRMMagic;
            public int U0; // u8
            public int U1;
            public int NumVertices;
            public int Stride; // was mislabeled "VerticesMode" -- it's the per-vertex byte stride
            public int U3;

            public byte[] Data;
        }

        public struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 Tangent;
            public Vector4 Color;
            public Vector2 UV;
        }

        public struct INDXStruct
        {
            public string INDXMagic;
            public int U0; // u8
            public int U1;
            public int IndexCount;
            public string IndexMode; // "ID16" or "ID32"
            public List<int> Indexs;
        }

        public struct MeshGroup
        {
            public Vector3 BboxMin;
            public Vector3 BboxMax;
            public int FirstTriangle;
            public int TriangleCount;

            public int FirstIndex => FirstTriangle * 3;
            public int IndexCount => TriangleCount * 3;
        }

        public struct MESHStruct
        {
            public int TriangleCount;
            public string Version; // "GEO3" (character) or "GEO4" (track proxy)
            public string Name;
            public Vector3 BboxMin;
            public Vector3 BboxMax;

            // Only populated for GEO4: N spatial groups, each its own bbox
            // plus a contiguous run of the index buffer (in TRIANGLES, not
            // indices -- the counts sum to TriangleCount).
            public List<MeshGroup> Groups;
        }

        public struct Faces
        {
            public Vertex V1;
            public Vertex V2;
            public Vertex V3;
        }
    }
}
