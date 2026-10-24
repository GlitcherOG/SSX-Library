using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SSXLibrary.Internal.Utilities;
using System.Numerics;

namespace SSXLibrary.FileHandlers.Models.SSXOG
{
    public class SSXMPFModelHandler
    {
        public int U1;
        public int HeaderCount;
        public int HeaderSize;
        public int FileStart;
        public List<MPFModelHeader> ModelList = new List<MPFModelHeader>();

        public void load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                U1 = StreamUtil.ReadUInt32(stream);
                HeaderCount = StreamUtil.ReadInt16(stream);
                HeaderSize = StreamUtil.ReadInt16(stream);
                FileStart = StreamUtil.ReadUInt32(stream);
                //Load Headers
                for (int i = 0; i < HeaderCount; i++)
                {
                    MPFModelHeader modelHeader = new MPFModelHeader();

                    modelHeader.FileName = StreamUtil.ReadString(stream, 16);
                    modelHeader.DataOffset = StreamUtil.ReadUInt32(stream);
                    modelHeader.EntrySize = StreamUtil.ReadUInt32(stream);
                    modelHeader.BoneOffset = StreamUtil.ReadUInt32(stream);
                    modelHeader.IKPointOffset = StreamUtil.ReadUInt32(stream);
                    modelHeader.ChunkOffsets = StreamUtil.ReadUInt32(stream);
                    modelHeader.DataStart = StreamUtil.ReadUInt32(stream);

                    modelHeader.ChunksCount = StreamUtil.ReadInt16(stream);
                    modelHeader.BoneCount = StreamUtil.ReadInt16(stream);
                    modelHeader.Unknown1 = StreamUtil.ReadUInt8(stream);
                    modelHeader.Unknown2 = StreamUtil.ReadUInt8(stream);
                    modelHeader.MaterialCount = StreamUtil.ReadUInt8(stream);
                    modelHeader.IKCount = StreamUtil.ReadUInt8(stream);
                    modelHeader.Unknown3 = StreamUtil.ReadUInt8(stream);
                    //Face models list the body bones their palette uses here (FileID, BoneID pairs)
                    modelHeader.UnknownBytes = StreamUtil.ReadBytes(stream, 15);
                    ModelList.Add(modelHeader);
                }

                //Read Matrix
                int StartPos = FileStart;
                for (int i = 0; i < ModelList.Count; i++)
                {
                    stream.Position = StartPos + ModelList[i].DataOffset;
                    MPFModelHeader modelHandler = ModelList[i];
                    modelHandler.Matrix = StreamUtil.ReadBytes(stream, ModelList[i].EntrySize);
                    ModelList[i] = modelHandler;
                }
            }

            //Read Matrix Data
            for (int i = 0; i < ModelList.Count; i++)
            {
                Stream streamMatrix = new MemoryStream();
                var Model = ModelList[i];
                streamMatrix.Write(ModelList[i].Matrix, 0, ModelList[i].Matrix.Length);

                //Read BodyObjects
                Model.materialDataList = new List<MaterialData>();
                streamMatrix.Position = 0;
                for (int b = 0; b < Model.MaterialCount; b++)
                {
                    MaterialData body = new MaterialData();
                    body.Name = StreamUtil.ReadString(streamMatrix, 4);
                    body.X = StreamUtil.ReadFloat(streamMatrix);
                    body.Y = StreamUtil.ReadFloat(streamMatrix);
                    body.Z = StreamUtil.ReadFloat(streamMatrix);
                    Model.materialDataList.Add(body);
                }

                //Read Bone Data
                streamMatrix.Position = Model.BoneOffset;
                var bones = new List<Bone>();
                for (int b = 0; b < Model.BoneCount; b++)
                {
                    Bone boneData = new Bone();
                    boneData.boneName = StreamUtil.ReadString(streamMatrix, 16);
                    boneData.BoneParentFileID = StreamUtil.ReadInt16(streamMatrix);
                    boneData.BoneParentID = StreamUtil.ReadInt16(streamMatrix);
                    boneData.Position = StreamUtil.ReadVector3(streamMatrix);
                    bones.Add(boneData);
                }
                Model.bone = bones;

                //Read IK Point?
                streamMatrix.Position = Model.IKPointOffset;
                var Verties = new List<Vector3>();
                for (int b = 0; b < Model.IKCount; b++)
                {
                    Vector3 v = StreamUtil.ReadVector3(streamMatrix);
                    streamMatrix.Position += 4;
                    Verties.Add(v);
                }
                Model.IKPoint = Verties;

                //Read Chunk Offsets
                streamMatrix.Position = Model.ChunkOffsets;
                Model.MaterialGroups = new List<MaterialGroup>();
                for (int b = 0; b < Model.ChunksCount; b++)
                {
                    MaterialGroup materialGroup = new MaterialGroup();
                    materialGroup.ID = StreamUtil.ReadUInt32(streamMatrix); //Flags, 1 Static Mesh, 2 Flex Mesh, 16 Shadow (No UVs or Normals)
                    materialGroup.MaterialID = StreamUtil.ReadUInt32(streamMatrix);
                    materialGroup.Unknown = StreamUtil.ReadUInt32(streamMatrix);
                    materialGroup.MeshOffset = StreamUtil.ReadUInt32(streamMatrix);
                    materialGroup.MeshOffsetEnd = StreamUtil.ReadUInt32(streamMatrix);
                    materialGroup.MorphMeshOffset = StreamUtil.ReadUInt32(streamMatrix);
                    materialGroup.MorphMeshOffsetEnd = StreamUtil.ReadUInt32(streamMatrix);
                    Model.MaterialGroups.Add(materialGroup);
                }

                streamMatrix.Position = Model.DataStart;
                //Read Static Model Data
                for (int n = 0; n < Model.MaterialGroups.Count; n++)
                {
                    var TempMaterialGroup = Model.MaterialGroups[n];
                    //Loads All Model Entries
                    TempMaterialGroup.staticMesh = new List<StaticMesh>();
                    if (TempMaterialGroup.MeshOffset != -1)
                    {
                        streamMatrix.Position = Model.MaterialGroups[n].MeshOffset;
                        while (true)
                        {
                            var ModelData = new StaticMesh();
                            //Load Main Model Data Header
                            streamMatrix.Position += 48;

                            if (streamMatrix.Position >= Model.MaterialGroups[n].MeshOffsetEnd)
                            {
                                break;
                            }
                            ModelData.StripCount = StreamUtil.ReadUInt32(streamMatrix);
                            ModelData.BoneAssigment = (StreamUtil.ReadUInt32(streamMatrix) - 14) / 4;
                            ModelData.Unknown2 = StreamUtil.ReadUInt32(streamMatrix);
                            ModelData.VertexCount = StreamUtil.ReadUInt32(streamMatrix);

                            //Load Strip Count
                            List<int> TempStrips = new List<int>();
                            for (int a = 0; a < ModelData.StripCount; a++)
                            {
                                TempStrips.Add(StreamUtil.ReadUInt32(streamMatrix));
                                streamMatrix.Position += 12;
                            }
                            streamMatrix.Position += 16;
                            ModelData.Strips = TempStrips;

                            List<Vector2> UVs = new List<Vector2>();
                            List<Vector3> Normals = new List<Vector3>();

                            //Read UV Texture Points
                            if ((TempMaterialGroup.ID & 16) == 0)
                            {
                                streamMatrix.Position += 48;
                                for (int a = 0; a < ModelData.VertexCount; a++)
                                {
                                    Vector2 uv = new Vector2();
                                    uv.X = StreamUtil.ReadInt16(streamMatrix) / 4096f;
                                    uv.Y = StreamUtil.ReadInt16(streamMatrix) / 4096f;
                                    UVs.Add(uv);
                                }
                                StreamUtil.AlignBy16(streamMatrix);
                               

                                streamMatrix.Position += 48;
                                for (int a = 0; a < ModelData.VertexCount; a++)
                                {
                                    Vector3 normal = new Vector3();
                                    normal.X = StreamUtil.ReadInt16(streamMatrix) / 32768f;
                                    normal.Y = StreamUtil.ReadInt16(streamMatrix) / 32768f;
                                    normal.Z = StreamUtil.ReadInt16(streamMatrix) / 32768f;
                                    Normals.Add(normal);
                                }
                                StreamUtil.AlignBy16(streamMatrix);
                            }
                            ModelData.uv = UVs;
                            ModelData.uvNormals = Normals;

                            List<Vector3> vertices = new List<Vector3>();
                            //Load Vertex
                            if (ModelData.VertexCount != 0)
                            {
                                streamMatrix.Position += 48;
                                for (int a = 0; a < ModelData.VertexCount; a++)
                                {
                                    Vector3 vertex = new Vector3();
                                    vertex = StreamUtil.ReadVector3(streamMatrix);
                                    vertices.Add(vertex);
                                }
                                StreamUtil.AlignBy16(streamMatrix);
                            }
                            ModelData.vertices = vertices;

                            streamMatrix.Position += 16;
                            ModelData = GenerateFacesStatic(ModelData);
                            TempMaterialGroup.staticMesh.Add(ModelData);
                        }
                    }

                    TempMaterialGroup.flexableMesh = new List<FlexableMesh>();
                    //Load Flex Mesh
                    if (TempMaterialGroup.MorphMeshOffset != -1)
                    {
                        streamMatrix.Position = Model.MaterialGroups[n].MorphMeshOffset;
                        while (true)
                        {
                            var modelSplitData = new FlexableMesh();
                            streamMatrix.Position += 48;
                            if (streamMatrix.Position >= Model.MaterialGroups[n].MorphMeshOffsetEnd)
                            {
                                break;
                            }

                            modelSplitData.BoneAssigmentCount = StreamUtil.ReadUInt32(streamMatrix);
                            modelSplitData.StripCount = StreamUtil.ReadUInt32(streamMatrix);
                            modelSplitData.VertexCount = StreamUtil.ReadUInt32(streamMatrix);
                            modelSplitData.NormalCount = StreamUtil.ReadUInt32(streamMatrix);

                            //Bone Blocks
                            //Each bone owns the next VertexCount positions followed by NormalCount normals in the vertex pool
                            var TempStrips = new List<NewSplit>();
                            for (int a = 0; a < modelSplitData.BoneAssigmentCount; a++)
                            {
                                NewSplit newSplit = new NewSplit();
                                newSplit.BoneAssignment = (StreamUtil.ReadUInt32(streamMatrix)-14)/4;
                                newSplit.VertexCount = StreamUtil.ReadUInt32(streamMatrix);
                                newSplit.NormalCount = StreamUtil.ReadUInt32(streamMatrix);
                                newSplit.NormalMatrix = StreamUtil.ReadUInt32(streamMatrix);
                                TempStrips.Add(newSplit);
                            }
                            modelSplitData.newSplits = TempStrips;

                            //Bone each pool entry is local to
                            List<int> PoolBones = new List<int>();
                            for (int a = 0; a < TempStrips.Count; a++)
                            {
                                for (int c = 0; c < TempStrips[a].VertexCount + TempStrips[a].NormalCount; c++)
                                {
                                    PoolBones.Add(TempStrips[a].BoneAssignment);
                                }
                            }

                            //Load Vertex Pool
                            streamMatrix.Position += 46;
                            int TempCount = StreamUtil.ReadUInt8(streamMatrix);
                            streamMatrix.Position += 1;
                            List<Vector3> vertices = new List<Vector3>();
                            for (int a = 0; a < TempCount; a++)
                            {
                                Vector3 vertex = StreamUtil.ReadVector3(streamMatrix);
                                vertices.Add(vertex);
                            }
                            modelSplitData.vertices = vertices;
                            StreamUtil.AlignBy16(streamMatrix);

                            //Load Weights
                            //First row is the 3 weight, 2 weight and 1 weight counts
                            //3 and 2 weight entries are a pool index row followed by a weight row, 1 weight entries are only a pool index row
                            streamMatrix.Position += 46;
                            TempCount = StreamUtil.ReadUInt8(streamMatrix);
                            streamMatrix.Position += 1;

                            modelSplitData.ThreeWeightCount = StreamUtil.ReadUInt16(streamMatrix);
                            modelSplitData.TwoWeightCount = StreamUtil.ReadUInt16(streamMatrix);
                            modelSplitData.OneWeightCount = StreamUtil.ReadUInt16(streamMatrix);

                            List<WeightedVertex> weightedVertices = new List<WeightedVertex>();
                            for (int a = 0; a < modelSplitData.ThreeWeightCount; a++)
                            {
                                weightedVertices.Add(ReadWeightedVertex(streamMatrix, 3, vertices, PoolBones));
                            }
                            for (int a = 0; a < modelSplitData.TwoWeightCount; a++)
                            {
                                weightedVertices.Add(ReadWeightedVertex(streamMatrix, 2, vertices, PoolBones));
                            }
                            for (int a = 0; a < modelSplitData.OneWeightCount; a++)
                            {
                                weightedVertices.Add(ReadWeightedVertex(streamMatrix, 1, vertices, PoolBones));
                            }
                            modelSplitData.weightedVertices = weightedVertices;
                            StreamUtil.AlignBy16(streamMatrix);

                            //Tristrip
                            streamMatrix.Position += 14;
                            TempCount = StreamUtil.ReadUInt8(streamMatrix);
                            streamMatrix.Position += 1;
                            List<int> ints = new List<int>();
                            for (int a = 0; a < TempCount; a++)
                            {
                                ints.Add(StreamUtil.ReadInt16(streamMatrix));
                            }
                            modelSplitData.Strips = ints;
                            StreamUtil.AlignBy16(streamMatrix);

                            //Strip Vertices
                            //Interlaced rows, each vertex is a weighted vertex index and pool normal index row followed by a UV row
                            //Shadow models (ID flag 16) only have the first row
                            streamMatrix.Position += 46;
                            TempCount = StreamUtil.ReadUInt8(streamMatrix);
                            streamMatrix.Position += 1;
                            if ((TempMaterialGroup.ID & 16) == 0)
                            {
                                TempCount = TempCount / 2;
                            }
                            List<FlexableVertex> stripVertices = new List<FlexableVertex>();
                            for (int a = 0; a < TempCount; a++)
                            {
                                FlexableVertex stripVertex = new FlexableVertex();
                                stripVertex.WeightIndex = StreamUtil.ReadUInt16(streamMatrix);
                                stripVertex.NormalIndex = StreamUtil.ReadUInt16(streamMatrix);
                                stripVertex.boneWeights = weightedVertices[stripVertex.WeightIndex].boneWeights;

                                if ((TempMaterialGroup.ID & 16) == 0)
                                {
                                    stripVertex.UV.X = StreamUtil.ReadInt16(streamMatrix) / 4096f;
                                    stripVertex.UV.Y = StreamUtil.ReadInt16(streamMatrix) / 4096f;
                                    stripVertex.Normal = vertices[stripVertex.NormalIndex];
                                    stripVertex.NormalBoneID = PoolBones[stripVertex.NormalIndex];
                                }
                                stripVertices.Add(stripVertex);
                            }
                            modelSplitData.stripVertices = stripVertices;
                            StreamUtil.AlignBy16(streamMatrix);
                            streamMatrix.Position += 16;

                            modelSplitData = GenerateFacesFlex(modelSplitData);

                            TempMaterialGroup.flexableMesh.Add(modelSplitData);
                        }
                    }

                    Model.MaterialGroups[n] = TempMaterialGroup;
                }

                ModelList[i] = Model;
                streamMatrix.Dispose();
                streamMatrix.Close();
            }
        }

        //WARNING - Claude assisted code
        //Save and the chunk writers below it were written with help from Claude.
        //Loading then saving every original model in CHAR HI and CHAR (74 files) gives back the same bytes,
        //apart from a few bytes of uninitialised padding after the chunk table which are written as 0.
        //
        //What it has to get right, none of which the reader needs to know about:
        // - Each mesh chunk is a PS2 DMA chain (cnt tags with their quad word counts) of VIF codes that unpack the data into VU1 memory.
        //   Static chunks interleave UV, normal and position with the cycle (3 rows a vertex), flex chunks unpack one block after another.
        // - Every unpack is lined up with NOPs so its data starts on a quad word, the minimum NOPs differ per block.
        // - Row 0 of each chunk header is a GIF tag, row 1 holds the matrix and normal matrix VU addresses (2 + 4 * Slot and 105 + 3 * Slot).
        // - Every chunk ends with a ref tag pointing at the kick (FLUSHE, MSCAL) at the end of its region, after a ret tag ending the chain.
        // - Shadow GIF tags and the unknown header bytes (the face models palette list) have to be written exactly as the game expects.
        public void Save(string path)
        {
            MemoryStream stream = new MemoryStream();

            FileStart = 12 + 64 * ModelList.Count;
            FileStart += (16 - FileStart % 16) % 16;
            stream.Position = FileStart;

            for (int i = 0; i < ModelList.Count; i++)
            {
                var Model = ModelList[i];
                MemoryStream ModelStream = new MemoryStream();

                //Materials
                for (int a = 0; a < Model.materialDataList.Count; a++)
                {
                    StreamUtil.WriteString(ModelStream, Model.materialDataList[a].Name, 4);
                    StreamUtil.WriteFloat32(ModelStream, Model.materialDataList[a].X);
                    StreamUtil.WriteFloat32(ModelStream, Model.materialDataList[a].Y);
                    StreamUtil.WriteFloat32(ModelStream, Model.materialDataList[a].Z);
                }
                StreamUtil.AlignBy16(ModelStream);

                //Bones
                Model.BoneOffset = (int)ModelStream.Position;
                for (int a = 0; a < Model.bone.Count; a++)
                {
                    StreamUtil.WriteString(ModelStream, Model.bone[a].boneName, 16);
                    StreamUtil.WriteInt16(ModelStream, Model.bone[a].BoneParentFileID);
                    StreamUtil.WriteInt16(ModelStream, Model.bone[a].BoneParentID);
                    StreamUtil.WriteVector3(ModelStream, Model.bone[a].Position);
                }

                //IK Points
                Model.IKPointOffset = (int)ModelStream.Position;
                for (int a = 0; a < Model.IKPoint.Count; a++)
                {
                    StreamUtil.WriteVector3(ModelStream, Model.IKPoint[a]);
                    StreamUtil.WriteInt32(ModelStream, 0);
                }

                //Chunk Offsets, written once the mesh data is
                Model.ChunkOffsets = (int)ModelStream.Position;
                ModelStream.Position += 28 * Model.MaterialGroups.Count;
                StreamUtil.AlignBy16(ModelStream);

                //Mesh Data
                Model.DataStart = (int)ModelStream.Position;
                for (int n = 0; n < Model.MaterialGroups.Count; n++)
                {
                    var TempMaterialGroup = Model.MaterialGroups[n];
                    bool Shadow = (TempMaterialGroup.ID & 16) != 0;

                    //Static Mesh
                    TempMaterialGroup.MeshOffset = -1;
                    TempMaterialGroup.MeshOffsetEnd = -1;
                    if ((TempMaterialGroup.ID & 1) != 0)
                    {
                        TempMaterialGroup.MeshOffset = (int)ModelStream.Position;
                        List<int> RefTags = new List<int>();
                        for (int a = 0; a < TempMaterialGroup.staticMesh.Count; a++)
                        {
                            WriteStaticMesh(ModelStream, TempMaterialGroup.staticMesh[a], Shadow, RefTags);
                        }
                        TempMaterialGroup.MeshOffsetEnd = WriteMeshEnd(ModelStream, TempMaterialGroup.MeshOffset, RefTags);
                    }

                    //Flex Mesh
                    TempMaterialGroup.MorphMeshOffset = -1;
                    TempMaterialGroup.MorphMeshOffsetEnd = -1;
                    if ((TempMaterialGroup.ID & 2) != 0)
                    {
                        TempMaterialGroup.MorphMeshOffset = (int)ModelStream.Position;
                        List<int> RefTags = new List<int>();
                        for (int a = 0; a < TempMaterialGroup.flexableMesh.Count; a++)
                        {
                            WriteFlexMesh(ModelStream, TempMaterialGroup.flexableMesh[a], Shadow, RefTags);
                        }
                        TempMaterialGroup.MorphMeshOffsetEnd = WriteMeshEnd(ModelStream, TempMaterialGroup.MorphMeshOffset, RefTags);
                    }

                    Model.MaterialGroups[n] = TempMaterialGroup;
                }

                //Chunk Offsets
                long EndPos = ModelStream.Position;
                ModelStream.Position = Model.ChunkOffsets;
                for (int a = 0; a < Model.MaterialGroups.Count; a++)
                {
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].ID);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].MaterialID);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].Unknown);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].MeshOffset);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].MeshOffsetEnd);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].MorphMeshOffset);
                    StreamUtil.WriteInt32(ModelStream, Model.MaterialGroups[a].MorphMeshOffsetEnd);
                }
                ModelStream.Position = EndPos;

                Model.ChunksCount = Model.MaterialGroups.Count;
                Model.BoneCount = Model.bone.Count;
                Model.MaterialCount = Model.materialDataList.Count;
                Model.IKCount = Model.IKPoint.Count;
                Model.DataOffset = (int)stream.Position - FileStart;
                Model.EntrySize = (int)ModelStream.Length;

                ModelStream.Position = 0;
                StreamUtil.WriteStreamIntoStream(stream, ModelStream);
                ModelStream.Dispose();
                ModelStream.Close();
                ModelList[i] = Model;
            }

            //Header
            stream.Position = 0;
            StreamUtil.WriteInt32(stream, U1);
            StreamUtil.WriteInt16(stream, ModelList.Count);
            StreamUtil.WriteInt16(stream, HeaderSize);
            StreamUtil.WriteInt32(stream, FileStart);

            for (int i = 0; i < ModelList.Count; i++)
            {
                var TempModel = ModelList[i];
                StreamUtil.WriteString(stream, TempModel.FileName, 16);
                StreamUtil.WriteInt32(stream, TempModel.DataOffset);
                StreamUtil.WriteInt32(stream, TempModel.EntrySize);
                StreamUtil.WriteInt32(stream, TempModel.BoneOffset);
                StreamUtil.WriteInt32(stream, TempModel.IKPointOffset);
                StreamUtil.WriteInt32(stream, TempModel.ChunkOffsets);
                StreamUtil.WriteInt32(stream, TempModel.DataStart);

                StreamUtil.WriteInt16(stream, TempModel.ChunksCount);
                StreamUtil.WriteInt16(stream, TempModel.BoneCount);
                StreamUtil.WriteUInt8(stream, TempModel.Unknown1);
                StreamUtil.WriteUInt8(stream, TempModel.Unknown2);
                StreamUtil.WriteUInt8(stream, TempModel.MaterialCount);
                StreamUtil.WriteUInt8(stream, TempModel.IKCount);
                StreamUtil.WriteUInt8(stream, TempModel.Unknown3);
                StreamUtil.WriteBytes(stream, TempModel.UnknownBytes ?? new byte[15]);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var file = File.Create(path);
            stream.Position = 0;
            stream.CopyTo(file);
            stream.Dispose();
            file.Close();
        }

        //Each chunk is a DMA chain of VIF codes that unpacks into VU1 memory, then a ref tag pointing at the kick at the end of the region
        void WriteStaticMesh(Stream ModelStream, StaticMesh Data, bool Shadow, List<int> RefTags)
        {
            int RowCount = Data.StripCount + 2;
            int Slot = Data.BoneAssigment + 3;

            //Header
            int TagPos = (int)ModelStream.Position;
            WriteDMATag(ModelStream, 1, 0);
            WriteUnpackStart(ModelStream, 1, 2);
            WriteVIFCode(ModelStream, 0x01, 0, 0x0101);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x6C, RowCount, 0x8000);

            WriteGIFTag(ModelStream, Data.VertexCount, Shadow);
            StreamUtil.WriteInt32(ModelStream, Data.StripCount);
            StreamUtil.WriteInt32(ModelStream, 2 + 4 * Slot);
            StreamUtil.WriteInt32(ModelStream, Shadow ? 0 : 105 + 3 * Slot);
            StreamUtil.WriteInt32(ModelStream, Data.VertexCount);
            for (int i = 0; i < Data.StripCount; i++)
            {
                StreamUtil.WriteInt32(ModelStream, Data.Strips[i + 1] - Data.Strips[i]);
                StreamUtil.WriteInt32(ModelStream, 0);
                StreamUtil.WriteInt32(ModelStream, 0);
                StreamUtil.WriteInt32(ModelStream, 0);
            }
            FinishDMATag(ModelStream, TagPos);

            //Vertex Data
            TagPos = (int)ModelStream.Position;
            WriteDMATag(ModelStream, 1, 0);
            if (!Shadow)
            {
                //UV, Normal and Position are interleaved by the cycle (3 rows a vertex)
                WriteUnpackStart(ModelStream, 1, 10);
                WriteSTROW(ModelStream, 0, 0, 4096, 4096);
                WriteSTMASK(ModelStream, 0x50505050);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x01, 0, 0x0103);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x75, Data.VertexCount, 0x8000 | RowCount);
                for (int i = 0; i < Data.VertexCount; i++)
                {
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.uv[i].X, 4096f));
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.uv[i].Y, 4096f));
                }

                WriteUnpackStart(ModelStream, 1, 10);
                WriteSTROW(ModelStream, 0, 0, 0, 0x8000);
                WriteSTMASK(ModelStream, 0x40404040);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x01, 0, 0x0103);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x79, Data.VertexCount, 0x8000 | (RowCount + 1));
                for (int i = 0; i < Data.VertexCount; i++)
                {
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.uvNormals[i].X, 32768f));
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.uvNormals[i].Y, 32768f));
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.uvNormals[i].Z, 32768f));
                }
                StreamUtil.AlignBy(ModelStream, 4);

                WriteUnpackStart(ModelStream, 1, 10);
                WriteSTROW(ModelStream, 0, 0, 0, 0x3F800000);
                WriteSTMASK(ModelStream, 0x40404040);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x01, 0, 0x0103);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x78, Data.VertexCount, 0x8000 | (RowCount + 2));
            }
            else
            {
                WriteUnpackStart(ModelStream, 1, 10);
                WriteSTROW(ModelStream, 0, 0, 0, 0x3F800000);
                WriteSTMASK(ModelStream, 0x40404040);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x01, 0, 0x0101);
                WriteVIFCode(ModelStream, 0x00);
                WriteVIFCode(ModelStream, 0x78, Data.VertexCount, 0x8000 | RowCount);
            }
            for (int i = 0; i < Data.VertexCount; i++)
            {
                StreamUtil.WriteVector3(ModelStream, Data.vertices[i]);
            }
            StreamUtil.AlignBy16(ModelStream);
            FinishDMATag(ModelStream, TagPos);

            WriteRefTag(ModelStream, RefTags);
        }

        void WriteFlexMesh(Stream ModelStream, FlexableMesh Data, bool Shadow, List<int> RefTags)
        {
            int RowCount = Data.newSplits.Count + 2;
            int WeightRows = 1 + 2 * Data.ThreeWeightCount + 2 * Data.TwoWeightCount + Data.OneWeightCount;
            int StripRows = Shadow ? Data.stripVertices.Count : Data.stripVertices.Count * 2;

            int TagPos = (int)ModelStream.Position;
            WriteDMATag(ModelStream, 1, 0);

            //Header
            WriteUnpackStart(ModelStream, 1, 2);
            WriteVIFCode(ModelStream, 0x01, 0, 0x0101);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x6C, RowCount, 0x8000);

            WriteGIFTag(ModelStream, Data.stripVertices.Count, Shadow);
            StreamUtil.WriteInt32(ModelStream, Data.newSplits.Count);
            StreamUtil.WriteInt32(ModelStream, Data.StripCount);
            StreamUtil.WriteInt32(ModelStream, Data.VertexCount);
            StreamUtil.WriteInt32(ModelStream, Data.NormalCount);
            for (int i = 0; i < Data.newSplits.Count; i++)
            {
                int Slot = Data.newSplits[i].BoneAssignment + 3;
                StreamUtil.WriteInt32(ModelStream, 2 + 4 * Slot);
                StreamUtil.WriteInt32(ModelStream, Data.newSplits[i].VertexCount);
                StreamUtil.WriteInt32(ModelStream, Data.newSplits[i].NormalCount);
                StreamUtil.WriteInt32(ModelStream, Shadow ? 0 : 105 + 3 * Slot);
            }

            //Vertex Pool
            WriteUnpackStart(ModelStream, 1, 10);
            WriteSTROW(ModelStream, 0, 0, 0, 0x3F800000);
            WriteSTMASK(ModelStream, 0x40404040);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x78, Data.vertices.Count, 0x8000 | RowCount);
            for (int i = 0; i < Data.vertices.Count; i++)
            {
                StreamUtil.WriteVector3(ModelStream, Data.vertices[i]);
            }

            //Weights
            WriteUnpackStart(ModelStream, 1, 10);
            WriteSTROW(ModelStream, 0, 0, 0, 0x8000);
            WriteSTMASK(ModelStream, 0x40404040);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x79, WeightRows, 0xC000 | (RowCount + Data.vertices.Count));
            StreamUtil.WriteInt16(ModelStream, Data.ThreeWeightCount);
            StreamUtil.WriteInt16(ModelStream, Data.TwoWeightCount);
            StreamUtil.WriteInt16(ModelStream, Data.OneWeightCount);
            for (int i = 0; i < Data.weightedVertices.Count; i++)
            {
                var Weights = Data.weightedVertices[i].boneWeights;
                for (int a = 0; a < 3; a++)
                {
                    StreamUtil.WriteInt16(ModelStream, a < Weights.Count ? Weights[a].VertexIndex : 0);
                }
                if (Weights.Count != 1)
                {
                    for (int a = 0; a < 3; a++)
                    {
                        StreamUtil.WriteInt16(ModelStream, a < Weights.Count ? (int)MathF.Round(Weights[a].Weight * 32768f) : 0);
                    }
                }
            }
            StreamUtil.AlignBy(ModelStream, 4);

            //Tristrip
            WriteUnpackStart(ModelStream, 3, 0);
            WriteVIFCode(ModelStream, 0x66, Data.StripCount, 0xC000 | (RowCount + Data.vertices.Count + WeightRows));
            for (int i = 0; i < Data.StripCount; i++)
            {
                StreamUtil.WriteUInt8(ModelStream, Data.Strips[i + 1] - Data.Strips[i]);
                StreamUtil.WriteUInt8(ModelStream, 0);
            }
            StreamUtil.AlignBy(ModelStream, 4);

            //Strip Vertices
            WriteUnpackStart(ModelStream, 1, 10);
            WriteSTROW(ModelStream, 0, 0, 4096, 4096);
            WriteSTMASK(ModelStream, 0x50505050);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x75, StripRows, 0x8000 | (RowCount + Data.vertices.Count + WeightRows + Data.StripCount));
            for (int i = 0; i < Data.stripVertices.Count; i++)
            {
                StreamUtil.WriteInt16(ModelStream, Data.stripVertices[i].WeightIndex);
                StreamUtil.WriteInt16(ModelStream, Data.stripVertices[i].NormalIndex);
                if (!Shadow)
                {
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.stripVertices[i].UV.X, 4096f));
                    StreamUtil.WriteInt16(ModelStream, Quantize(Data.stripVertices[i].UV.Y, 4096f));
                }
            }
            StreamUtil.AlignBy16(ModelStream);
            FinishDMATag(ModelStream, TagPos);

            WriteRefTag(ModelStream, RefTags);
        }

        //Ret tag ending the chain, then the kick (FLUSHE, MSCAL) every ref tag in the region points at
        int WriteMeshEnd(Stream ModelStream, int RegionStart, List<int> RefTags)
        {
            WriteDMATag(ModelStream, 6, 1);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x01, 0, 0x0101);

            int RegionEnd = (int)ModelStream.Position;
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x10);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x14);

            for (int i = 0; i < RefTags.Count; i++)
            {
                ModelStream.Position = RefTags[i] + 4;
                StreamUtil.WriteInt32(ModelStream, RegionEnd - RegionStart);
            }
            ModelStream.Position = RegionEnd + 16;
            return RegionEnd;
        }

        static void WriteRefTag(Stream ModelStream, List<int> RefTags)
        {
            RefTags.Add((int)ModelStream.Position);
            WriteDMATag(ModelStream, 3, 1);
            WriteVIFCode(ModelStream, 0x00);
            WriteVIFCode(ModelStream, 0x00);
        }

        static void WriteDMATag(Stream ModelStream, int ID, int QWC, int Address = 0)
        {
            StreamUtil.WriteInt32(ModelStream, (ID << 28) | QWC);
            StreamUtil.WriteInt32(ModelStream, Address);
        }

        //Quad word count of a cnt tag is everything after the tag up to the current position
        static void FinishDMATag(Stream ModelStream, int TagPos)
        {
            int EndPos = (int)ModelStream.Position;
            ModelStream.Position = TagPos;
            StreamUtil.WriteInt32(ModelStream, (1 << 28) | ((EndPos - TagPos) / 16 - 1));
            ModelStream.Position = EndPos;
        }

        static void WriteVIFCode(Stream ModelStream, int Command, int Num = 0, int Immediate = 0)
        {
            StreamUtil.WriteInt32(ModelStream, (Command << 24) | ((Num & 0xFF) << 16) | Immediate);
        }

        static void WriteSTROW(Stream ModelStream, int X, int Y, int Z, int W)
        {
            WriteVIFCode(ModelStream, 0x30);
            StreamUtil.WriteInt32(ModelStream, X);
            StreamUtil.WriteInt32(ModelStream, Y);
            StreamUtil.WriteInt32(ModelStream, Z);
            StreamUtil.WriteInt32(ModelStream, W);
        }

        static void WriteSTMASK(Stream ModelStream, int Mask)
        {
            WriteVIFCode(ModelStream, 0x20);
            StreamUtil.WriteInt32(ModelStream, Mask);
        }

        //NOPs so the unpack lands on the last word of a quad word and its data starts aligned
        static void WriteUnpackStart(Stream ModelStream, int MinNOPs, int CodeWords)
        {
            for (int i = 0; i < MinNOPs; i++)
            {
                WriteVIFCode(ModelStream, 0x00);
            }
            while ((ModelStream.Position / 4 + CodeWords) % 4 != 3)
            {
                WriteVIFCode(ModelStream, 0x00);
            }
        }

        static void WriteGIFTag(Stream ModelStream, int VertexCount, bool Shadow)
        {
            StreamUtil.WriteInt32(ModelStream, 0x8000 | VertexCount);
            StreamUtil.WriteInt32(ModelStream, Shadow ? 0x10224000 : 0x302E4000);
            StreamUtil.WriteInt32(ModelStream, Shadow ? 0x4 : 0x412);
            StreamUtil.WriteInt32(ModelStream, 0);
        }

        static int Quantize(float Value, float Scale)
        {
            return (int)Math.Clamp(MathF.Round(Value * Scale), short.MinValue, short.MaxValue);
        }

        public WeightedVertex ReadWeightedVertex(Stream stream, int WeightCount, List<Vector3> vertices, List<int> PoolBones)
        {
            WeightedVertex weightedVertex = new WeightedVertex();
            weightedVertex.boneWeights = new List<BoneWeight>();

            int[] PoolIndex = new int[3];
            for (int i = 0; i < 3; i++)
            {
                PoolIndex[i] = StreamUtil.ReadUInt16(stream);
            }

            //Weights are out of 32768
            float[] Weights = new float[3] { 1f, 0f, 0f };
            if (WeightCount != 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    Weights[i] = StreamUtil.ReadUInt16(stream) / 32768f;
                }
            }

            for (int i = 0; i < WeightCount; i++)
            {
                BoneWeight boneWeight = new BoneWeight();
                boneWeight.VertexIndex = PoolIndex[i];
                boneWeight.Position = vertices[PoolIndex[i]];
                boneWeight.BoneID = PoolBones[PoolIndex[i]];
                boneWeight.Weight = Weights[i];
                weightedVertex.boneWeights.Add(boneWeight);
            }

            return weightedVertex;
        }

        public StaticMesh GenerateFacesStatic(StaticMesh models)
        {
            var ModelData = models;
            //Increment Strips
            List<int> strip2 = new List<int>();
            strip2.Add(0);
            foreach (var item in ModelData.Strips)
            {
                strip2.Add(strip2[strip2.Count - 1] + item);
            }
            ModelData.Strips = strip2;

            //Make Faces
            ModelData.faces = new List<Face>();
            int localIndex = 0;
            int Rotation = 0;
            for (int b = 0; b < ModelData.vertices.Count; b++)
            {
                if (InsideSplits(b, ModelData.Strips))
                {
                    Rotation = 0;
                    localIndex = 1;
                    continue;
                }
                if (localIndex < 2)
                {
                    localIndex++;
                    continue;
                }

                ModelData.faces.Add(CreateFacesStatic(b, ModelData, Rotation));
                Rotation++;
                if (Rotation == 2)
                {
                    Rotation = 0;
                }
                localIndex++;
            }

            return ModelData;
        }

        public bool InsideSplits(int Number, List<int> splits)
        {
            foreach (var item in splits)
            {
                if (item == Number)
                {
                    return true;
                }
            }
            return false;
        }
        public Face CreateFacesStatic(int Index, StaticMesh ModelData, int roatation)
        {
            Face face = new Face();
            int Index1 = 0;
            int Index2 = 0;
            int Index3 = 0;

            face.BoneAssigment = ModelData.BoneAssigment;

            //Fixes the Rotation For Exporting
            //Swap When Exporting to other formats
            //1-Clockwise
            //0-Counter Clocwise
            if (roatation == 1)
            {
                Index1 = Index;
                Index2 = Index - 1;
                Index3 = Index - 2;
            }
            if (roatation == 0)
            {
                Index1 = Index;
                Index2 = Index - 2;
                Index3 = Index - 1;
            }
            face.V1 = ModelData.vertices[Index1];
            face.V2 = ModelData.vertices[Index2];
            face.V3 = ModelData.vertices[Index3];

            face.V1Pos = Index1;
            face.V2Pos = Index2;
            face.V3Pos = Index3;

            if (ModelData.uv.Count != 0)
            {
                face.UV1 = ModelData.uv[Index1];
                face.UV2 = ModelData.uv[Index2];
                face.UV3 = ModelData.uv[Index3];

                face.UV1Pos = Index1;
                face.UV2Pos = Index2;
                face.UV3Pos = Index3;

                face.Normal1 = ModelData.uvNormals[Index1];
                face.Normal2 = ModelData.uvNormals[Index2];
                face.Normal3 = ModelData.uvNormals[Index3];

                face.Normal1Pos = Index1;
                face.Normal2Pos = Index2;
                face.Normal3Pos = Index3;
            }

            return face;
        }

        public FlexableMesh GenerateFacesFlex(FlexableMesh models)
        {
            var ModelData = models;
            //Increment Strips
            List<int> strip2 = new List<int>();
            strip2.Add(0);
            foreach (var item in ModelData.Strips)
            {
                strip2.Add(strip2[strip2.Count - 1] + item);
            }
            ModelData.Strips = strip2;

            //Make Faces
            ModelData.faces = new List<FlexableFace>();
            int localIndex = 0;
            int Rotation = 0;
            for (int b = 0; b < ModelData.stripVertices.Count; b++)
            {
                if (InsideSplits(b, ModelData.Strips))
                {
                    Rotation = 0;
                    localIndex = 1;
                    continue;
                }
                if (localIndex < 2)
                {
                    localIndex++;
                    continue;
                }

                ModelData.faces.Add(CreateFacesFlex(b, ModelData, Rotation));
                Rotation++;
                if (Rotation == 2)
                {
                    Rotation = 0;
                }
                localIndex++;
            }

            return ModelData;
        }

        public FlexableFace CreateFacesFlex(int Index, FlexableMesh ModelData, int roatation)
        {
            FlexableFace face = new FlexableFace();
            int Index1 = 0;
            int Index2 = 0;
            int Index3 = 0;

            //Fixes the Rotation For Exporting
            //Swap When Exporting to other formats
            //1-Clockwise
            //0-Counter Clocwise
            if (roatation == 1)
            {
                Index1 = Index;
                Index2 = Index - 1;
                Index3 = Index - 2;
            }
            if (roatation == 0)
            {
                Index1 = Index - 2;
                Index2 = Index - 1;
                Index3 = Index;
            }

            face.V1Pos = Index1;
            face.V2Pos = Index2;
            face.V3Pos = Index3;

            face.V1 = ModelData.stripVertices[Index1];
            face.V2 = ModelData.stripVertices[Index2];
            face.V3 = ModelData.stripVertices[Index3];

            return face;
        }


        public struct MPFModelHeader
        {
            //Main Header
            public string FileName;
            public int DataOffset;
            public int EntrySize;
            public int BoneOffset;
            public int IKPointOffset;
            public int ChunkOffsets;
            public int DataStart;
            //Counts
            public int ChunksCount;
            public int BoneCount;
            public int Unknown1;
            public int Unknown2;
            public int MaterialCount;
            public int IKCount;
            public int Unknown3;
            public byte[] UnknownBytes;

            public byte[] Matrix;

            public List<Vector3> IKPoint;
            public List<MaterialGroup> MaterialGroups;
            public List<MaterialData> materialDataList;
            public List<Bone> bone;
        }

        public struct FlexableMesh
        {
            public int BoneAssigmentCount;
            public int StripCount;
            public int VertexCount; //Total positions in the pool
            public int NormalCount; //Total normals in the pool

            public List<NewSplit> newSplits;

            //Pool of bone local positions and normals, laid out in newSplits order
            public List<Vector3> vertices;

            public int ThreeWeightCount;
            public int TwoWeightCount;
            public int OneWeightCount;
            public List<WeightedVertex> weightedVertices;

            public List<int> Strips;
            public List<FlexableVertex> stripVertices;

            public List<FlexableFace> faces;

        }

        public struct NewSplit
        {
            public int BoneAssignment;
            public int VertexCount;
            public int NormalCount;
            public int NormalMatrix; //VU address of the bones rotation matrix (105 + 3 * (BoneAssignment + 3))
        }

        public struct WeightedVertex
        {
            public List<BoneWeight> boneWeights;
        }

        public struct BoneWeight
        {
            public int BoneID;
            public float Weight;
            public int VertexIndex; //Pool index
            public Vector3 Position; //Local to BoneID
        }

        public struct FlexableVertex
        {
            public int WeightIndex;
            public int NormalIndex; //Pool index
            public List<BoneWeight> boneWeights;

            public Vector3 Normal; //Local to NormalBoneID
            public int NormalBoneID;
            public Vector2 UV;
        }
        public struct StaticMesh
        {
            public int StripCount;
            public int BoneAssigment;
            public int Unknown2;
            public int VertexCount;

            public List<int> Strips;
            public List<Vector2> uv;
            public List<Vector3> vertices;
            public List<Vector3> uvNormals;
            public List<Face> faces;
        }
        public struct Bone
        {
            public string boneName;
            public int BoneParentFileID;
            public int BoneParentID;
            public Vector3 Position;
            public Vector3 Radians;
        }

        public struct MaterialGroup
        {
            public int ID;
            public int MaterialID;
            public int Unknown;
            public int MeshOffset;
            public int MeshOffsetEnd;
            public int MorphMeshOffset;
            public int MorphMeshOffsetEnd;

            public List<StaticMesh> staticMesh;
            public List<FlexableMesh> flexableMesh;
        }

        public struct Face
        {
            public Vector3 V1;
            public Vector3 V2;
            public Vector3 V3;

            public int V1Pos;
            public int V2Pos;
            public int V3Pos;

            public Vector2 UV1;
            public Vector2 UV2;
            public Vector2 UV3;

            public int UV1Pos;
            public int UV2Pos;
            public int UV3Pos;

            public Vector3 Normal1;
            public Vector3 Normal2;
            public Vector3 Normal3;

            public int Normal1Pos;
            public int Normal2Pos;
            public int Normal3Pos;

            public int BoneAssigment;
        }

        public struct FlexableFace
        {
            public FlexableVertex V1;
            public FlexableVertex V2;
            public FlexableVertex V3;

            public int V1Pos;
            public int V2Pos;
            public int V3Pos;
        }

        public struct MaterialData
        {
            public string Name;
            public float X;
            public float Y;
            public float Z;
        }
    }
}
