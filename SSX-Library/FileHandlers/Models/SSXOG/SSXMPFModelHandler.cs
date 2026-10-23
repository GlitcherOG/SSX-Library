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
                    stream.Position += 15;
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
