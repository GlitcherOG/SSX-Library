//WARNING - Claude assisted code
//This combiner (and the flex mesh reading in SSXMPFModelHandler) was written with help from Claude.
//It has been checked against every character in CHAR HI, but give it a bit more suspicion than the rest of the library.
//
//Why it wasnt done by hand, the SSX 2000 model format is a pain to work with:
// - Bones only store a name, parent and offset. No rotations at all, so the bind pose has to be solved
//   back out of the mesh data. The glTF keeps the mesh in that bind pose and poses the bones into the Tricky A Pose.
// - Flex meshes give every bone weighted to a vertex its own copy of that vertex in that bones space,
//   so one vertex can be stored up to three times in three different places.
// - Strip vertices are interlaced rows (weight index and normal index, then the UV) on top of the vertex pool,
//   weight records and strip lengths, all as separate VIF unpacks.
// - Mesh bone IDs point at a 25 slot matrix palette rather than the bone list. Face models get their own palette
//   whose order changes per character, so the slots have to be worked out from the data.
// - Shadow models carry their own slightly different copy of the extra bones (Elises ponytail is flattened onto the head).
// - Some bones have no blended vertices at all so their rotation cant be recovered from their own data.
//   The eyes borrow Jurgens right eye (the only solvable one), the lo sweater and ponytail borrow the hi ones and Kaoris backpack borrows the Tricky one.
//Working all of that out by hand was not doing wonders for my sanity, hence the help.

using SSXLibrary.Internal.Utilities;
using SharpTriStrip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.FileHandlers.Models.SSXOG
{
    public class SSXOGModelCombiner
    {
        public SSXMPFModelHandler? Body;
        public SSXMPFModelHandler? Head;
        public SSXMPFModelHandler? Board;
        public List<SSXMPFModelHandler.MaterialData> materials;
        public List<BoneData> bones;
        public bool BodyBool;

        public List<ReassignedMesh> reassignedMesh = new List<ReassignedMesh>();

        //Mesh bone IDs point into a 25 slot matrix palette rather than the models own bone list
        //0-18 Body Bones, 19 Board, 20-24 Extra Bones (Head file extras first then Body file extras)
        //Face models have more bones than the extra slots so they use their own palette,
        //their own bones first then the body bones they blend with (order changes per character)
        public const int BodyBoneCount = 19;
        public const int BoardSlot = 19;
        public const int ExtraSlotStart = 20;
        public const int SlotCount = 25;

        //MeshID -> LOD in the model names, the hi files have 5000, the lo files have 2500, 1200 and 750, both have the shadow
        public static readonly string[] MeshLODs = { "5000", "2500", "1200", "750", "Shdw" };

        public int[] GlobalSlots;
        public List<int[]> BodySlotMaps;
        public List<int[]> HeadSlotMaps;

        List<bool> BonePlaced;
        Dictionary<string, Quaternion> ReferenceRotations = new Dictionary<string, Quaternion>();
        Dictionary<int, Quaternion> FallbackRotations = new Dictionary<int, Quaternion>();
        //(Bone Name, Parent Name) -> Position from the other LODs
        Dictionary<(string, string), Vector3> OtherLODBones = new Dictionary<(string, string), Vector3>();

        //Tricky A Pose, Tricky uses the same skeleton as SSX 2000 and its bind pose lines up with these within a few degrees
        //Used as the glTF pose, for the hips and for any bone that cant be solved
        //Applied the same way as SaveTrickyPS2glTF (Euler.ToQuaternion of the negated radians)
        static readonly Dictionary<string, Vector3> TrickyRotations = new Dictionary<string, Vector3>
        {
            { "hips", new Vector3(-1.570796f, 0f, 0f) },
            { "lower_back", new Vector3(-1.570796f, -0.2028998f, -1.570796f) },
            { "upper_back", new Vector3(0f, 0f, -0.5403033f) },
            { "neck", new Vector3(0f, 0f, -0.01540061f) },
            { "head", new Vector3(0f, 0f, 0.1637997f) },
            { "l_clav", new Vector3(-1.570796f, 1.207595f, -2.886101f) },
            { "l_bicep", new Vector3(1.513255f, -0.05744576f, 0.2005471f) },
            { "l_forearm", new Vector3(0f, 0f, 0.3752056f) },
            { "l_hand", new Vector3(0f, 0f, -0.02876756f) },
            { "r_clav", new Vector3(-1.570796f, -1.212519f, -2.886101f) },
            { "r_bicep", new Vector3(1.628338f, -0.05744559f, -0.2054636f) },
            { "r_forearm", new Vector3(0f, 0f, 0.3752056f) },
            { "r_hand", new Vector3(0f, 0f, -0.02876756f) },
            { "l_thigh", new Vector3(1.521636f, 0.2825688f, 1.572404f) },
            { "l_calf", new Vector3(0f, 0f, -0.7012873f) },
            { "l_foot", new Vector3(0.3389621f, 0.03983452f, 1.591451f) },
            { "r_thigh", new Vector3(1.620065f, 0.284082f, 1.577144f) },
            { "r_calf", new Vector3(0f, 0f, -0.7011843f) },
            { "r_foot", new Vector3(-0.4348488f, -0.03099652f, 1.567695f) },
            //Kaori, rigid only in SSX 2000 so it cant be solved
            { "backpack", new Vector3(0f, 0f, -2.975751f) },
        };

        //Bones that cant be solved in every file, taken from the file where they can be (local to the parent)
        static readonly Dictionary<string, Quaternion> KnownRotations = new Dictionary<string, Quaternion>
        {
            //Eyes are rigid on almost every character, this is Jurgens right eye (the one eye with blended vertices)
            //Looks down X with Y down and fits the eye sockets of every character
            { "r_eye", new Quaternion(0.003451718f, -0.004110118f, 0.6386722f, 0.7694602f) },
            { "l_eye", new Quaternion(0.003451718f, -0.004110118f, 0.6386722f, 0.7694602f) },
            //Zoes sweater from zoe_body_hi, the lo sweater doesnt blend with the body
            { "l_top", new Quaternion(-0.4333153f, 0.5587822f, -0.4333166f, 0.5587819f) },
            { "r_top", new Quaternion(-0.4494641f, 0.5371504f, -0.4165416f, 0.5796073f) },
            //Elises ponytail from elise_head_hi (solved from its flattened shadow ponytail), the lo shadow ponytail isnt flattened
            { "epony_1", new Quaternion(0.2486002f, -0.6619651f, -0.6619653f, 0.2486003f) },
        };

        public int DectectModelType(SSXMPFModelHandler modelHandler)
        {
            for (int i = 0; i < modelHandler.ModelList.Count; i++)
            {
                if (modelHandler.ModelList[i].FileName.ToLower().Contains("body"))
                {
                    Board = null;
                    Body = modelHandler;
                    BodyBool = true;
                    return 0;
                }
                if (modelHandler.ModelList[i].FileName.ToLower().Contains("head"))
                {
                    Board = null;
                    Head = modelHandler;
                    BodyBool = true;
                    return 1;
                }
                if (modelHandler.ModelList[i].FileName.ToLower().Contains("algoofy"))
                {
                    Head = null;
                    Body = null;
                    Board = modelHandler;
                    BodyBool = false;
                    return 2;
                }
            }
            return -1;
        }

        //0 - Body Skeleton, 1 - Face, 2 - Uses the shared palette
        public int ModelPalette(SSXMPFModelHandler.MPFModelHeader modelHeader)
        {
            if (modelHeader.BoneCount > SlotCount - ExtraSlotStart)
            {
                if (modelHeader.bone[0].BoneParentID == -1)
                {
                    return 0;
                }
                return 1;
            }
            return 2;
        }

        public void StartReassignMesh(int MeshID, bool HighModel)
        {
            if (MeshID != -1)
            {
                if (BodyBool)
                {
                    if (Head != null && Body != null)
                    {
                        if(HighModel)
                        {
                            if(MeshID==1)
                            {
                                MeshID = 4;
                            }
                        }
                        else
                        {
                            MeshID++;
                        }

                        StartReassignMeshCharacter(MeshID);
                    }
                }
                else if (Board != null)
                {
                    StartReassignMeshBoard(MeshID);
                }
            }
        }

        //MeshID is the model in the board file
        public void StartReassignMeshBoard(int MeshID)
        {
            reassignedMesh = new List<ReassignedMesh>();
            materials = new List<SSXMPFModelHandler.MaterialData>();
            bones = new List<BoneData>();
            BonePlaced = new List<bool>();

            var Model = Board.ModelList[MeshID];
            materials.AddRange(Model.materialDataList);
            for (int i = 0; i < Model.bone.Count; i++)
            {
                AddBone(Model.bone[i].boneName, Model.bone[i].BoneParentID, Model.bone[i].Position);
            }
            UpdateWorldMatrices();

            //Board meshes point at the board slot of the palette
            int[] SlotMap = Enumerable.Repeat(-1, SlotCount).ToArray();
            SlotMap[BoardSlot] = 0;

            var TempMesh = new ReassignedMesh();
            TempMesh.MeshName = Model.FileName;
            TempMesh.ShadowModel = Model.FileName.ToLower().Contains("shdw");
            TempMesh.faces = ReturnFixedFaces(Model, SlotMap, false);
            TempMesh.IKPoints = Model.IKPoint;
            reassignedMesh.Add(TempMesh);
        }

        public void StartReassignMeshCharacter(int MeshID)
        {
            reassignedMesh = new List<ReassignedMesh>();
            materials = new List<SSXMPFModelHandler.MaterialData>();

            //Shadow models have no blended vertices to solve the extra bones from, borrow them from the most detailed LOD
            ReferenceRotations = new Dictionary<string, Quaternion>();
            int ReferenceLOD = Enumerable.Range(0, MeshLODs.Length - 1).FirstOrDefault(x => HasMeshLOD(x), MeshID);
            if (MeshID != ReferenceLOD)
            {
                GenerateBones(ReferenceLOD);
                CorrectBoneRotations(ReferenceLOD);
                for (int i = 0; i < bones.Count; i++)
                {
                    ReferenceRotations[bones[i].BoneName] = Quaternion.CreateFromRotationMatrix(bones[i].WorldMatrix);
                }
            }

            GenerateBones(MeshID);
            CorrectBoneRotations(MeshID);

            //Body
            var AddedMeshes = new HashSet<string>();
            for (int i = 0; i < Body.ModelList.Count; i++)
            {
                AddReassignedMesh(Body.ModelList[i], BodySlotMaps[i], MeshID, AddedMeshes);
            }

            //Head
            AddedMeshes = new HashSet<string>();
            for (int i = 0; i < Head.ModelList.Count; i++)
            {
                AddReassignedMesh(Head.ModelList[i], HeadSlotMaps[i], MeshID, AddedMeshes);
            }
        }

        #region Regen
        //WARNING - Claude assisted code
        //The mesh rebuilding was written with help from Claude. Every character, board and LOD in CHAR HI and CHAR has been
        //exported, reimported and rebuilt with every original face coming back with the same winding, but it has not been tested in game yet.
        //Why it does what it does:
        // - Chunks are sized off the largest counts the original files use and the 181 quad word VU1 buffer, going over corrupts the next chunk.
        // - Static chunks hold one bone so only faces fully weighted to a single slot go static, everything else goes flex.
        // - Flex vertices need a copy of the position in every weighted bones space, these come from the solved bind pose the glTF mesh is kept in.
        //   If the glTF skin has a different bind pose (the bones got moved in whatever edited it) the mesh gets moved back into ours first.
        // - NvTriStrip merges double sided faces, flips winding and drops or invents faces next to edges with 3+ faces,
        //   so double sided faces get their own pass, every strip triangle is checked against the input and anything missing is added back.
        //VU1 input buffer, every chunk type in the original files fills it up to 181 quad words
        public const int MaxChunkRows = 181;
        //Largest counts the original files use per chunk
        const int MaxStaticVerts = 59;
        const int MaxStaticShadowVerts = 162;
        const int MaxStaticStrips = 17;
        const int MaxStaticShadowStrips = 34;
        const int MaxFlexBones = 13;
        const int MaxFlexPool = 80;
        const int MaxFlexWeightRows = 48;
        const int MaxFlexStrips = 9;
        const int MaxFlexShadowStrips = 13;
        const int MaxFlexStripVerts = 39;
        const int MaxFlexShadowStripVerts = 59;

        public void StartRegenMesh(SSXOGModelCombiner ImportCombiner, int MeshID, bool HighModel)
        {
            if (MeshID != -1 && ImportCombiner != null)
            {
                if (BodyBool)
                {
                    if (Head != null && Body != null)
                    {
                        if (HighModel)
                        {
                            if (MeshID == 1)
                            {
                                MeshID = 4;
                            }
                        }
                        else
                        {
                            MeshID++;
                        }

                        StartRegenMeshCharacter(ImportCombiner, MeshID);
                    }
                }
                else if (Board != null)
                {
                    StartRegenMeshBoard(ImportCombiner, MeshID);
                }
            }
        }

        public void StartRegenMeshCharacter(SSXOGModelCombiner ImportCombiner, int MeshID)
        {
            //Same skeleton and materials the export used
            StartReassignMeshCharacter(MeshID);
            int[] BoneMap = MapImportBones(ImportCombiner);
            List<int> MaterialMap = MapImportMaterials(ImportCombiner);
            var ImportMeshes = ImportToBindPose(ImportCombiner, BoneMap);

            for (int i = 0; i < ImportMeshes.Count; i++)
            {
                var ImportMesh = ImportMeshes[i];
                if (!RegenModels(Body, BodySlotMaps, ImportMesh, MeshID, BoneMap, MaterialMap) &&
                    !RegenModels(Head, HeadSlotMaps, ImportMesh, MeshID, BoneMap, MaterialMap))
                {
                    //MessageBox.Show("Incorrectly Named Or Unneeded mesh part detected \n" + ImportMesh.MeshName);
                }
            }
        }

        public void StartRegenMeshBoard(SSXOGModelCombiner ImportCombiner, int MeshID)
        {
            StartReassignMeshBoard(MeshID);
            int[] BoneMap = MapImportBones(ImportCombiner);
            List<int> MaterialMap = MapImportMaterials(ImportCombiner);

            var ImportMeshes = ImportToBindPose(ImportCombiner, BoneMap);

            int[] SlotMap = Enumerable.Repeat(-1, SlotCount).ToArray();
            SlotMap[BoardSlot] = 0;

            for (int i = 0; i < ImportMeshes.Count; i++)
            {
                if (ImportMeshes[i].MeshName.Trim().ToLower() == Board.ModelList[MeshID].FileName.Trim().ToLower())
                {
                    Board.ModelList[MeshID] = RegenModel(Board.ModelList[MeshID], ImportMeshes[i], SlotMap, BoneMap, MaterialMap, true);
                }
            }
        }

        //The imported mesh is in the bind pose of its skin, which is ours unless the bones got moved in whatever edited it
        List<ReassignedMesh> ImportToBindPose(SSXOGModelCombiner ImportCombiner, int[] BoneMap)
        {
            //How each bone moved from our bind pose to the imported one
            var Moves = new Dictionary<int, Matrix4x4>();
            for (int i = 0; i < ImportCombiner.bones.Count; i++)
            {
                if (BoneMap[i] == -1)
                {
                    continue;
                }
                Matrix4x4.Invert(bones[BoneMap[i]].WorldMatrix, out Matrix4x4 BindInverse);
                Matrix4x4 Move = BindInverse * ImportCombiner.bones[i].WorldMatrix;
                if (!NearlyIdentity(Move))
                {
                    Moves.Add(i, Move);
                }
            }
            if (Moves.Count == 0)
            {
                return ImportCombiner.reassignedMesh;
            }

            var Meshes = new List<ReassignedMesh>();
            for (int i = 0; i < ImportCombiner.reassignedMesh.Count; i++)
            {
                var TempMesh = ImportCombiner.reassignedMesh[i];
                TempMesh.faces = new List<ReassignedFace>(TempMesh.faces);
                for (int a = 0; a < TempMesh.faces.Count; a++)
                {
                    var Face = TempMesh.faces[a];
                    (Face.V1, Face.Normal1) = VertexToBindPose(Face.V1, Face.Normal1, Face.Weight1, Moves);
                    (Face.V2, Face.Normal2) = VertexToBindPose(Face.V2, Face.Normal2, Face.Weight2, Moves);
                    (Face.V3, Face.Normal3) = VertexToBindPose(Face.V3, Face.Normal3, Face.Weight3, Moves);
                    TempMesh.faces[a] = Face;
                }
                Meshes.Add(TempMesh);
            }
            return Meshes;
        }

        static (Vector3, Vector3) VertexToBindPose(Vector3 Position, Vector3 Normal, SSXMPFModelHandler.WeightedVertex Weight, Dictionary<int, Matrix4x4> Moves)
        {
            //Untouched bones leave the vertex exactly as it is
            if (!Weight.boneWeights.Any(x => Moves.ContainsKey(x.BoneID)))
            {
                return (Position, Normal);
            }

            //Skinning blends the bone moves together, inverting the blend undoes it exactly
            Matrix4x4 Blend = new Matrix4x4();
            float Total = 0;
            for (int i = 0; i < Weight.boneWeights.Count; i++)
            {
                Blend += Moves.GetValueOrDefault(Weight.boneWeights[i].BoneID, Matrix4x4.Identity) * Weight.boneWeights[i].Weight;
                Total += Weight.boneWeights[i].Weight;
            }
            if (!Matrix4x4.Invert(Blend * (1f / Total), out Matrix4x4 Undo))
            {
                return (Position, Normal);
            }
            return (Vector3.Transform(Position, Undo), Normal == Vector3.Zero ? Normal : Vector3.Normalize(Vector3.TransformNormal(Normal, Undo)));
        }

        static bool NearlyIdentity(Matrix4x4 Matrix)
        {
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (MathF.Abs(Matrix[x, y] - Matrix4x4.Identity[x, y]) > 0.001f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        bool RegenModels(SSXMPFModelHandler modelHandler, List<int[]> SlotMaps, ReassignedMesh ImportMesh, int MeshID, int[] BoneMap, List<int> MaterialMap)
        {
            for (int i = 0; i < modelHandler.ModelList.Count; i++)
            {
                var Model = modelHandler.ModelList[i];
                if (InMeshLOD(Model, MeshID) && Model.FileName.Trim().ToLower() == ImportMesh.MeshName.Trim().ToLower())
                {
                    modelHandler.ModelList[i] = RegenModel(Model, ImportMesh, SlotMaps[i], BoneMap, MaterialMap, false);
                    return true;
                }
            }
            return false;
        }

        //Imported bone -> combined bone by name
        int[] MapImportBones(SSXOGModelCombiner ImportCombiner)
        {
            int[] BoneMap = new int[ImportCombiner.bones.Count];
            for (int i = 0; i < ImportCombiner.bones.Count; i++)
            {
                BoneMap[i] = bones.FindIndex(x => x.BoneName.ToLower() == ImportCombiner.bones[i].BoneName.ToLower());
            }
            return BoneMap;
        }

        //Imported material -> combined material, using the names the export gives them (repeats get .1, .2)
        List<int> MapImportMaterials(SSXOGModelCombiner ImportCombiner)
        {
            var ExportNames = new List<string>();
            for (int i = 0; i < materials.Count; i++)
            {
                int NameCount = materials.Take(i).Count(x => x.Name == materials[i].Name);
                ExportNames.Add(NameCount == 0 ? materials[i].Name : materials[i].Name + "." + NameCount);
            }

            var MaterialMap = new List<int>();
            for (int i = 0; i < ImportCombiner.materials.Count; i++)
            {
                string Name = ImportCombiner.materials[i].Name;
                int Index = ExportNames.IndexOf(Name);
                if (Index == -1)
                {
                    //New material, use the texture name before any .1
                    string BaseName = Name.Split('.')[0];
                    Index = materials.FindIndex(x => x.Name == BaseName);
                    if (Index == -1)
                    {
                        var NewMaterial = new SSXMPFModelHandler.MaterialData();
                        NewMaterial.Name = BaseName.Length > 4 ? BaseName.Substring(0, 4) : BaseName;
                        NewMaterial.X = 127f / 255f;
                        NewMaterial.Y = 127f / 255f;
                        NewMaterial.Z = 127f / 255f;
                        materials.Add(NewMaterial);
                        Index = materials.Count - 1;
                    }
                }
                MaterialMap.Add(Index);
            }
            return MaterialMap;
        }

        public SSXMPFModelHandler.MPFModelHeader RegenModel(SSXMPFModelHandler.MPFModelHeader Model, ReassignedMesh ImportMesh, int[] SlotMap, int[] BoneMap, List<int> MaterialMap, bool BoardMaterials)
        {
            bool Shadow = Model.MaterialGroups.Count != 0 ? (Model.MaterialGroups[0].ID & 16) != 0 : Model.FileName.ToLower().Contains("shdw");

            //Combined bone -> slot
            var BoneSlots = new Dictionary<int, int>();
            for (int i = 0; i < SlotMap.Length; i++)
            {
                if (SlotMap[i] != -1 && !BoneSlots.ContainsKey(SlotMap[i]))
                {
                    BoneSlots.Add(SlotMap[i], i);
                }
            }

            //Imported material -> model material, boards keep theirs in the same order as the export
            Model.materialDataList = new List<SSXMPFModelHandler.MaterialData>(Model.materialDataList);
            int[] ModelMaterials = new int[MaterialMap.Count];
            for (int i = 0; i < MaterialMap.Count; i++)
            {
                var Material = materials[MaterialMap[i]];
                int Index = BoardMaterials && MaterialMap[i] < Model.materialDataList.Count ? MaterialMap[i] :
                    Model.materialDataList.FindIndex(x => x.Name == Material.Name && x.X == Material.X && x.Y == Material.Y && x.Z == Material.Z);
                if (Index == -1)
                {
                    Model.materialDataList.Add(Material);
                    Index = Model.materialDataList.Count - 1;
                }
                ModelMaterials[i] = Index;
            }

            var Faces = new List<RegenVertex[]>();
            var FaceMaterials = new List<int>();
            for (int i = 0; i < ImportMesh.faces.Count; i++)
            {
                var Face = ImportMesh.faces[i];
                Faces.Add(new RegenVertex[]
                {
                    MakeRegenVertex(Face.V1, Face.Normal1, Face.UV1, Face.Weight1, BoneSlots, BoneMap),
                    MakeRegenVertex(Face.V2, Face.Normal2, Face.UV2, Face.Weight2, BoneSlots, BoneMap),
                    MakeRegenVertex(Face.V3, Face.Normal3, Face.UV3, Face.Weight3, BoneSlots, BoneMap),
                });
                FaceMaterials.Add(ModelMaterials[Face.MaterialID]);
            }

            //One group per material, faces all on one bone are static, the rest flex
            Model.MaterialGroups = new List<SSXMPFModelHandler.MaterialGroup>();
            foreach (int Material in FaceMaterials.Distinct())
            {
                var GroupFaces = Enumerable.Range(0, Faces.Count).Where(x => FaceMaterials[x] == Material).Select(x => Faces[x]).ToList();
                var MaterialGroup = new SSXMPFModelHandler.MaterialGroup();
                MaterialGroup.MaterialID = Material;
                MaterialGroup.Unknown = -1;
                MaterialGroup.staticMesh = new List<SSXMPFModelHandler.StaticMesh>();
                MaterialGroup.flexableMesh = new List<SSXMPFModelHandler.FlexableMesh>();

                var StaticFaces = GroupFaces.Where(x => x.All(y => y.Slots.Length == 1 && y.Slots[0] == x[0].Slots[0])).ToList();
                foreach (int Slot in StaticFaces.Select(x => x[0].Slots[0]).Distinct())
                {
                    MaterialGroup.staticMesh.AddRange(GenerateStaticChunks(StaticFaces.Where(x => x[0].Slots[0] == Slot).ToList(), Slot, SlotMap[Slot], Shadow));
                }
                MaterialGroup.flexableMesh.AddRange(GenerateFlexChunks(GroupFaces.Except(StaticFaces).ToList(), SlotMap, Shadow));

                MaterialGroup.ID = (MaterialGroup.staticMesh.Count != 0 ? 1 : 0) | (MaterialGroup.flexableMesh.Count != 0 ? 2 : 0) | (Shadow ? 16 : 0);
                Model.MaterialGroups.Add(MaterialGroup);
            }

            return Model;
        }

        RegenVertex MakeRegenVertex(Vector3 Position, Vector3 Normal, Vector2 UV, SSXMPFModelHandler.WeightedVertex Weight, Dictionary<int, int> BoneSlots, int[] BoneMap)
        {
            //Bones outside the models palette use their nearest parent that is in it
            var SlotWeights = new Dictionary<int, float>();
            for (int i = 0; i < Weight.boneWeights.Count; i++)
            {
                int ImportBone = Weight.boneWeights[i].BoneID;
                if (ImportBone < 0 || ImportBone >= BoneMap.Length || BoneMap[ImportBone] == -1)
                {
                    continue;
                }
                int Slot = FindSlot(BoneMap[ImportBone], BoneSlots);
                SlotWeights[Slot] = SlotWeights.GetValueOrDefault(Slot) + Weight.boneWeights[i].Weight;
            }
            if (SlotWeights.Count == 0)
            {
                SlotWeights[BoneSlots.Values.Min()] = 1f;
            }

            //Up to 3 weights out of 32768, the original files dont always add up to exactly 32768 so only fix ones that are off
            var Top = SlotWeights.OrderByDescending(x => x.Value).Take(3).ToList();
            float Total = Top.Sum(x => x.Value);
            int[] Weights;
            if (MathF.Abs(Total - 1f) < 0.001f)
            {
                Weights = Top.Select(x => (int)MathF.Round(x.Value * 32768f)).ToArray();
            }
            else
            {
                Weights = Top.Select(x => (int)MathF.Round(x.Value / Total * 32768f)).ToArray();
                Weights[0] += 32768 - Weights.Sum();
            }

            RegenVertex NewVertex = new RegenVertex();
            NewVertex.Position = Position;
            NewVertex.Normal = Normal;
            NewVertex.UV = UV;
            NewVertex.Slots = Top.Where((x, i) => Weights[i] > 0).Select(x => x.Key).ToArray();
            NewVertex.Weights = Weights.Where(x => x > 0).ToArray();
            return NewVertex;
        }

        int FindSlot(int Bone, Dictionary<int, int> BoneSlots)
        {
            while (Bone != -1)
            {
                if (BoneSlots.TryGetValue(Bone, out int Slot))
                {
                    return Slot;
                }
                Bone = bones[Bone].ParentBone;
            }
            return BoneSlots.Values.Min();
        }

        List<SSXMPFModelHandler.StaticMesh> GenerateStaticChunks(List<RegenVertex[]> Faces, int Slot, int Bone, bool Shadow)
        {
            Matrix4x4.Invert(bones[Bone].WorldMatrix, out Matrix4x4 Inverse);

            //Back into the bones space
            var Vertices = new List<(Vector3 Position, Vector3 Normal, Vector2 UV)>();
            var Lookup = new Dictionary<(Vector3, Vector3, Vector2), int>();
            var Indices = new List<int>();
            for (int i = 0; i < Faces.Count; i++)
            {
                for (int a = 0; a < 3; a++)
                {
                    var Key = (Vector3.Transform(Faces[i][a].Position, Inverse), LocalNormal(Faces[i][a].Normal, Inverse), Faces[i][a].UV);
                    if (!Lookup.TryGetValue(Key, out int Index))
                    {
                        Index = Vertices.Count;
                        Vertices.Add(Key);
                        Lookup.Add(Key, Index);
                    }
                    Indices.Add(Index);
                }
            }

            int MaxVerts = Shadow ? MaxStaticShadowVerts : MaxStaticVerts;
            int MaxStrips = Shadow ? MaxStaticShadowStrips : MaxStaticStrips;
            int VertexRows = Shadow ? 1 : 3;
            int MaxLength = Math.Min(MaxVerts, (MaxChunkRows - 3) / VertexRows);

            //Pack strips into chunks that fit in VU memory
            var Chunks = new List<List<List<int>>>();
            var Chunk = new List<List<int>>();
            int ChunkVerts = 0;
            foreach (var Strip in SplitStrips(GenerateStrips(Indices), MaxLength))
            {
                if (Chunk.Count != 0 && (Chunk.Count + 1 > MaxStrips || ChunkVerts + Strip.Count > MaxVerts ||
                    2 + Chunk.Count + 1 + (ChunkVerts + Strip.Count) * VertexRows > MaxChunkRows))
                {
                    Chunks.Add(Chunk);
                    Chunk = new List<List<int>>();
                    ChunkVerts = 0;
                }
                Chunk.Add(Strip);
                ChunkVerts += Strip.Count;
            }
            if (Chunk.Count != 0)
            {
                Chunks.Add(Chunk);
            }

            var StaticMeshes = new List<SSXMPFModelHandler.StaticMesh>();
            var ModelHandler = new SSXMPFModelHandler();
            for (int i = 0; i < Chunks.Count; i++)
            {
                var Data = new SSXMPFModelHandler.StaticMesh();
                Data.StripCount = Chunks[i].Count;
                Data.BoneAssigment = Slot;
                Data.Unknown2 = Shadow ? 0 : 105 + 3 * (Slot + 3);
                Data.Strips = Chunks[i].Select(x => x.Count).ToList();
                Data.vertices = new List<Vector3>();
                Data.uv = new List<Vector2>();
                Data.uvNormals = new List<Vector3>();
                foreach (var Strip in Chunks[i])
                {
                    foreach (int Index in Strip)
                    {
                        Data.vertices.Add(Vertices[Index].Position);
                        if (!Shadow)
                        {
                            Data.uv.Add(Vertices[Index].UV);
                            Data.uvNormals.Add(Vertices[Index].Normal);
                        }
                    }
                }
                Data.VertexCount = Data.vertices.Count;
                StaticMeshes.Add(ModelHandler.GenerateFacesStatic(Data));
            }
            return StaticMeshes;
        }

        List<SSXMPFModelHandler.FlexableMesh> GenerateFlexChunks(List<RegenVertex[]> Faces, int[] SlotMap, bool Shadow)
        {
            var FlexMeshes = new List<SSXMPFModelHandler.FlexableMesh>();
            if (Faces.Count == 0)
            {
                return FlexMeshes;
            }

            var Inverses = new Dictionary<int, Matrix4x4>();
            Matrix4x4 SlotInverse(int Slot)
            {
                if (!Inverses.TryGetValue(Slot, out Matrix4x4 Inverse))
                {
                    Matrix4x4.Invert(bones[SlotMap[Slot]].WorldMatrix, out Inverse);
                    Inverses.Add(Slot, Inverse);
                }
                return Inverse;
            }

            //Each weight gets its own copy of the position in that bones space, the normal goes in the biggest weights bone
            var Vertices = new List<FlexVertex>();
            var Lookup = new Dictionary<string, int>();
            var Indices = new List<int>();
            for (int i = 0; i < Faces.Count; i++)
            {
                for (int a = 0; a < 3; a++)
                {
                    var Vertex = Faces[i][a];
                    FlexVertex NewVertex = new FlexVertex();
                    NewVertex.Slots = Vertex.Slots;
                    NewVertex.Weights = Vertex.Weights;
                    NewVertex.Positions = Vertex.Slots.Select(x => Vector3.Transform(Vertex.Position, SlotInverse(x))).ToArray();
                    NewVertex.NormalSlot = Vertex.Slots[0];
                    NewVertex.Normal = Shadow ? Vector3.Zero : LocalNormal(Vertex.Normal, SlotInverse(Vertex.Slots[0]));
                    NewVertex.UV = Shadow ? Vector2.Zero : Vertex.UV;
                    NewVertex.RecordKey = string.Join("|", Enumerable.Range(0, Vertex.Slots.Length).Select(x => $"{Vertex.Slots[x]},{NewVertex.Positions[x]},{NewVertex.Weights[x]}"));

                    string Key = NewVertex.RecordKey + "|" + NewVertex.NormalSlot + "|" + NewVertex.Normal + "|" + NewVertex.UV;
                    if (!Lookup.TryGetValue(Key, out int Index))
                    {
                        Index = Vertices.Count;
                        Vertices.Add(NewVertex);
                        Lookup.Add(Key, Index);
                    }
                    Indices.Add(Index);
                }
            }

            //Pack strips into chunks that fit in VU memory
            int MaxStripVerts = Shadow ? MaxFlexShadowStripVerts : MaxFlexStripVerts;
            var Chunks = new List<FlexChunk>();
            var Chunk = new FlexChunk(Shadow);
            foreach (var Strip in SplitStrips(GenerateStrips(Indices), MaxStripVerts))
            {
                if (Chunk.TryAdd(Strip, Vertices))
                {
                    continue;
                }
                if (Chunk.Strips.Count != 0)
                {
                    Chunks.Add(Chunk);
                    Chunk = new FlexChunk(Shadow);
                }

                //Strip too big for an empty chunk on its own, shorten it until it fits
                var Remaining = Strip;
                while (!Chunk.TryAdd(Remaining, Vertices))
                {
                    int Length = (Remaining.Count - 1) & ~1;
                    while (Length > 4 && !Chunk.Fits(Remaining.GetRange(0, Length), Vertices))
                    {
                        Length -= 2;
                    }
                    Chunk.TryAdd(Remaining.GetRange(0, Length), Vertices);
                    Chunks.Add(Chunk);
                    Chunk = new FlexChunk(Shadow);
                    Remaining = Remaining.GetRange(Length - 2, Remaining.Count - Length + 2);
                }
            }
            if (Chunk.Strips.Count != 0)
            {
                Chunks.Add(Chunk);
            }

            var ModelHandler = new SSXMPFModelHandler();
            for (int i = 0; i < Chunks.Count; i++)
            {
                FlexMeshes.Add(ModelHandler.GenerateFacesFlex(Chunks[i].Build(Vertices)));
            }
            return FlexMeshes;
        }

        static Vector3 LocalNormal(Vector3 Normal, Matrix4x4 Inverse)
        {
            if (Normal == Vector3.Zero)
            {
                return Normal;
            }
            return Vector3.Normalize(Vector3.TransformNormal(Normal, Inverse));
        }

        //Tristrips in the winding the faces were given in
        static List<List<int>> GenerateStrips(List<int> Indices)
        {
            //Double sided faces (same 3 vertices both ways round) get merged by the tristripper,
            //so the back faces get stripped on their own. Exact repeats are dropped
            var Layers = new List<List<int>>();
            var LayerFaces = new List<HashSet<(int, int, int)>>();
            var LayerWinding = new List<HashSet<(int, int, int)>>();
            for (int i = 0; i < Indices.Count; i += 3)
            {
                int A = Indices[i], B = Indices[i + 1], C = Indices[i + 2];
                var Sorted = new[] { A, B, C }.OrderBy(x => x).ToArray();
                var Face = (Sorted[0], Sorted[1], Sorted[2]);
                var Winding = A < B && A < C ? (A, B, C) : B < C ? (B, C, A) : (C, A, B);

                int Layer = 0;
                bool Repeat = false;
                while (Layer < Layers.Count && LayerFaces[Layer].Contains(Face))
                {
                    if (LayerWinding[Layer].Contains(Winding))
                    {
                        Repeat = true;
                        break;
                    }
                    Layer++;
                }
                if (Repeat)
                {
                    continue;
                }
                if (Layer == Layers.Count)
                {
                    Layers.Add(new List<int>());
                    LayerFaces.Add(new HashSet<(int, int, int)>());
                    LayerWinding.Add(new HashSet<(int, int, int)>());
                }
                Layers[Layer].AddRange(new[] { A, B, C });
                LayerFaces[Layer].Add(Face);
                LayerWinding[Layer].Add(Winding);
            }

            var Strips = new List<List<int>>();
            for (int i = 0; i < Layers.Count; i++)
            {
                Strips.AddRange(GenerateLayerStrips(Layers[i]));
            }
            return Strips;
        }

        static List<List<int>> GenerateLayerStrips(List<int> Indices)
        {
            var Strips = new List<List<int>>();
            var FaceSet = new HashSet<(int, int, int)>();
            for (int i = 0; i < Indices.Count; i += 3)
            {
                FaceSet.Add((Indices[i], Indices[i + 1], Indices[i + 2]));
                FaceSet.Add((Indices[i + 1], Indices[i + 2], Indices[i]));
                FaceSet.Add((Indices[i + 2], Indices[i], Indices[i + 1]));
            }

            var Groups = TristripGenerator.ToTriangleStrips(Indices.Select(x => (ushort)x).ToArray(), false, 16, false);
            if (Groups == null)
            {
                for (int i = 0; i < Indices.Count; i += 3)
                {
                    Strips.Add(Indices.GetRange(i, 3));
                }
                return Strips;
            }

            for (int i = 0; i < Groups.Length; i++)
            {
                var Group = Groups[i].Indices.Select(x => (int)x).ToList();
                if (Groups[i].Type != TriStrip.PrimitiveType.Strip)
                {
                    for (int a = 0; a + 2 < Group.Count; a += 3)
                    {
                        Strips.Add(Group.GetRange(a, 3));
                    }
                    continue;
                }

                //Faces next to each other can be wound opposite ways (shadows), so check every face and start
                //a new strip where the winding goes wrong. A repeated first vertex starts a strip flipped
                List<int> Current = null;
                for (int a = 0; a + 2 < Group.Count; a++)
                {
                    int A = Group[a], B = Group[a + 1], C = Group[a + 2];
                    if (A == B || B == C || A == C)
                    {
                        Current?.Add(C);
                        continue;
                    }
                    bool Forward = FaceSet.Contains((A, B, C));
                    if (!Forward && !FaceSet.Contains((B, A, C)))
                    {
                        //Not one of the faces (the tristripper gets these wrong next to edges shared by 3 or more faces)
                        if (Current != null)
                        {
                            Strips.Add(Current);
                        }
                        Current = null;
                        continue;
                    }
                    if (Current != null)
                    {
                        bool Even = (Current.Count - 2) % 2 == 0;
                        if (Even == Forward)
                        {
                            Current.Add(C);
                            continue;
                        }
                        Strips.Add(Current);
                    }
                    Current = Forward ? new List<int> { A, B, C } : new List<int> { A, A, B, C };
                }
                if (Current != null)
                {
                    Strips.Add(Current);
                }
            }

            //The tristripper can drop faces on edges shared by 3 or more faces, give any it missed their own strip
            var Stripped = new HashSet<(int, int, int)>();
            foreach (var Strip in Strips)
            {
                for (int a = 0; a + 2 < Strip.Count; a++)
                {
                    var Face = a % 2 == 0 ? (Strip[a], Strip[a + 1], Strip[a + 2]) : (Strip[a + 1], Strip[a], Strip[a + 2]);
                    Stripped.Add(Face);
                    Stripped.Add((Face.Item2, Face.Item3, Face.Item1));
                    Stripped.Add((Face.Item3, Face.Item1, Face.Item2));
                }
            }
            for (int i = 0; i < Indices.Count; i += 3)
            {
                if (!Stripped.Contains((Indices[i], Indices[i + 1], Indices[i + 2])))
                {
                    Strips.Add(Indices.GetRange(i, 3));
                }
            }
            return Strips;
        }

        //Splits strips longer than the limit, the next piece starts 2 back at an even offset so the winding carries on
        static List<List<int>> SplitStrips(List<List<int>> Strips, int MaxLength)
        {
            int PieceLength = MaxLength % 2 == 0 ? MaxLength : MaxLength - 1;
            var NewStrips = new List<List<int>>();
            foreach (var Strip in Strips)
            {
                int Start = 0;
                while (Strip.Count - Start > MaxLength)
                {
                    NewStrips.Add(Strip.GetRange(Start, PieceLength));
                    Start += PieceLength - 2;
                }
                NewStrips.Add(Strip.GetRange(Start, Strip.Count - Start));
            }
            return NewStrips;
        }

        struct RegenVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;
            public int[] Slots;
            public int[] Weights; //Out of 32768
        }

        struct FlexVertex
        {
            public int[] Slots;
            public int[] Weights;
            public Vector3[] Positions; //Local to each slot
            public int NormalSlot;
            public Vector3 Normal; //Local to NormalSlot
            public Vector2 UV;
            public string RecordKey;
        }

        //Tracks what a flex chunk uses so strips are only added while it still fits
        class FlexChunk
        {
            bool Shadow;
            public List<List<int>> Strips = new List<List<int>>();
            List<int> Slots = new List<int>();
            Dictionary<int, List<Vector3>> Positions = new Dictionary<int, List<Vector3>>();
            Dictionary<int, List<Vector3>> Normals = new Dictionary<int, List<Vector3>>();
            List<string> Records = new List<string>();
            int[] RecordCounts = new int[4];
            int StripVerts;

            public FlexChunk(bool shadow)
            {
                Shadow = shadow;
            }

            public bool Fits(List<int> Strip, List<FlexVertex> Vertices)
            {
                var NewSlots = new HashSet<int>(Slots);
                var NewPositions = Positions.ToDictionary(x => x.Key, x => new HashSet<Vector3>(x.Value));
                var NewNormals = Normals.ToDictionary(x => x.Key, x => new HashSet<Vector3>(x.Value));
                var NewRecords = new HashSet<string>(Records);
                int[] NewCounts = (int[])RecordCounts.Clone();

                foreach (int Index in Strip)
                {
                    var Vertex = Vertices[Index];
                    for (int i = 0; i < Vertex.Slots.Length; i++)
                    {
                        NewSlots.Add(Vertex.Slots[i]);
                        if (!NewPositions.ContainsKey(Vertex.Slots[i]))
                        {
                            NewPositions.Add(Vertex.Slots[i], new HashSet<Vector3>());
                        }
                        NewPositions[Vertex.Slots[i]].Add(Vertex.Positions[i]);
                    }
                    if (!Shadow)
                    {
                        NewSlots.Add(Vertex.NormalSlot);
                        if (!NewNormals.ContainsKey(Vertex.NormalSlot))
                        {
                            NewNormals.Add(Vertex.NormalSlot, new HashSet<Vector3>());
                        }
                        NewNormals[Vertex.NormalSlot].Add(Vertex.Normal);
                    }
                    if (NewRecords.Add(Vertex.RecordKey))
                    {
                        NewCounts[Vertex.Slots.Length]++;
                    }
                }

                int Pool = NewPositions.Sum(x => x.Value.Count) + NewNormals.Sum(x => x.Value.Count);
                int WeightRows = 1 + 2 * NewCounts[3] + 2 * NewCounts[2] + NewCounts[1];
                int StripCount = Strips.Count + 1;
                int NewStripVerts = StripVerts + Strip.Count;
                int Rows = 2 + NewSlots.Count + Pool + WeightRows + StripCount + (Shadow ? NewStripVerts : NewStripVerts * 2);

                return NewSlots.Count <= MaxFlexBones && Pool <= MaxFlexPool && WeightRows <= MaxFlexWeightRows &&
                    StripCount <= (Shadow ? MaxFlexShadowStrips : MaxFlexStrips) &&
                    NewStripVerts <= (Shadow ? MaxFlexShadowStripVerts : MaxFlexStripVerts) && Rows <= MaxChunkRows;
            }

            public bool TryAdd(List<int> Strip, List<FlexVertex> Vertices)
            {
                if (!Fits(Strip, Vertices))
                {
                    return false;
                }

                foreach (int Index in Strip)
                {
                    var Vertex = Vertices[Index];
                    for (int i = 0; i < Vertex.Slots.Length; i++)
                    {
                        if (!Slots.Contains(Vertex.Slots[i]))
                        {
                            Slots.Add(Vertex.Slots[i]);
                            Positions.Add(Vertex.Slots[i], new List<Vector3>());
                            Normals.Add(Vertex.Slots[i], new List<Vector3>());
                        }
                        if (!Positions[Vertex.Slots[i]].Contains(Vertex.Positions[i]))
                        {
                            Positions[Vertex.Slots[i]].Add(Vertex.Positions[i]);
                        }
                    }
                    if (!Shadow)
                    {
                        if (!Slots.Contains(Vertex.NormalSlot))
                        {
                            Slots.Add(Vertex.NormalSlot);
                            Positions.Add(Vertex.NormalSlot, new List<Vector3>());
                            Normals.Add(Vertex.NormalSlot, new List<Vector3>());
                        }
                        if (!Normals[Vertex.NormalSlot].Contains(Vertex.Normal))
                        {
                            Normals[Vertex.NormalSlot].Add(Vertex.Normal);
                        }
                    }
                    if (!Records.Contains(Vertex.RecordKey))
                    {
                        Records.Add(Vertex.RecordKey);
                        RecordCounts[Vertex.Slots.Length]++;
                    }
                }
                Strips.Add(Strip);
                StripVerts += Strip.Count;
                return true;
            }

            public SSXMPFModelHandler.FlexableMesh Build(List<FlexVertex> Vertices)
            {
                var Data = new SSXMPFModelHandler.FlexableMesh();

                //Pool is each bones positions then its normals
                Data.newSplits = new List<SSXMPFModelHandler.NewSplit>();
                Data.vertices = new List<Vector3>();
                var PositionIndex = new Dictionary<(int, Vector3), int>();
                var NormalIndex = new Dictionary<(int, Vector3), int>();
                foreach (int Slot in Slots)
                {
                    var NewSplit = new SSXMPFModelHandler.NewSplit();
                    NewSplit.BoneAssignment = Slot;
                    NewSplit.VertexCount = Positions[Slot].Count;
                    NewSplit.NormalCount = Normals[Slot].Count;
                    NewSplit.NormalMatrix = Shadow ? 0 : 105 + 3 * (Slot + 3);
                    Data.newSplits.Add(NewSplit);

                    foreach (var Position in Positions[Slot])
                    {
                        PositionIndex.Add((Slot, Position), Data.vertices.Count);
                        Data.vertices.Add(Position);
                    }
                    foreach (var Normal in Normals[Slot])
                    {
                        NormalIndex.Add((Slot, Normal), Data.vertices.Count);
                        Data.vertices.Add(Normal);
                    }
                }

                //Weights, 3 weight then 2 weight then 1 weight
                var RecordVertex = new Dictionary<string, FlexVertex>();
                foreach (var Strip in Strips)
                {
                    foreach (int Index in Strip)
                    {
                        RecordVertex.TryAdd(Vertices[Index].RecordKey, Vertices[Index]);
                    }
                }
                var OrderedRecords = Records.OrderByDescending(x => RecordVertex[x].Slots.Length).ToList();
                var RecordIndex = new Dictionary<string, int>();
                Data.weightedVertices = new List<SSXMPFModelHandler.WeightedVertex>();
                foreach (var Record in OrderedRecords)
                {
                    var Vertex = RecordVertex[Record];
                    var WeightedVertex = new SSXMPFModelHandler.WeightedVertex();
                    WeightedVertex.boneWeights = new List<SSXMPFModelHandler.BoneWeight>();
                    for (int i = 0; i < Vertex.Slots.Length; i++)
                    {
                        var BoneWeight = new SSXMPFModelHandler.BoneWeight();
                        BoneWeight.BoneID = Vertex.Slots[i];
                        BoneWeight.Weight = Vertex.Weights[i] / 32768f;
                        BoneWeight.VertexIndex = PositionIndex[(Vertex.Slots[i], Vertex.Positions[i])];
                        BoneWeight.Position = Vertex.Positions[i];
                        WeightedVertex.boneWeights.Add(BoneWeight);
                    }
                    RecordIndex.Add(Record, Data.weightedVertices.Count);
                    Data.weightedVertices.Add(WeightedVertex);
                }

                //Strip Vertices
                Data.stripVertices = new List<SSXMPFModelHandler.FlexableVertex>();
                Data.Strips = new List<int>();
                foreach (var Strip in Strips)
                {
                    Data.Strips.Add(Strip.Count);
                    foreach (int Index in Strip)
                    {
                        var Vertex = Vertices[Index];
                        var StripVertex = new SSXMPFModelHandler.FlexableVertex();
                        StripVertex.WeightIndex = RecordIndex[Vertex.RecordKey];
                        StripVertex.boneWeights = Data.weightedVertices[StripVertex.WeightIndex].boneWeights;
                        if (!Shadow)
                        {
                            StripVertex.NormalIndex = NormalIndex[(Vertex.NormalSlot, Vertex.Normal)];
                            StripVertex.Normal = Vertex.Normal;
                            StripVertex.NormalBoneID = Vertex.NormalSlot;
                            StripVertex.UV = Vertex.UV;
                        }
                        Data.stripVertices.Add(StripVertex);
                    }
                }

                Data.BoneAssigmentCount = Data.newSplits.Count;
                Data.StripCount = Strips.Count;
                Data.VertexCount = Data.newSplits.Sum(x => x.VertexCount);
                Data.NormalCount = Data.newSplits.Sum(x => x.NormalCount);
                Data.ThreeWeightCount = RecordCounts[3];
                Data.TwoWeightCount = RecordCounts[2];
                Data.OneWeightCount = RecordCounts[1];
                return Data;
            }
        }
        #endregion

        public bool InMeshLOD(SSXMPFModelHandler.MPFModelHeader modelHeader, int MeshID)
        {
            string Name = modelHeader.FileName.ToLower();
            bool Shadow = Name.Contains("shdw");
            if (MeshID == MeshLODs.Length - 1)
            {
                return Shadow;
            }
            return !Shadow && Name.Contains(MeshLODs[MeshID]);
        }

        public bool HasMeshLOD(int MeshID)
        {
            return Body != null && Head != null && Body.ModelList.Any(x => InMeshLOD(x, MeshID)) && Head.ModelList.Any(x => InMeshLOD(x, MeshID));
        }

        public void AddReassignedMesh(SSXMPFModelHandler.MPFModelHeader modelHeader, int[] SlotMap, int MeshID, HashSet<string> AddedMeshes)
        {
            bool Shadow = modelHeader.FileName.ToLower().Contains("shdw");
            //Some files have a second copy of a model under the same name (jp_head_lo HeadBasic750), only use the first
            if (InMeshLOD(modelHeader, MeshID) && AddedMeshes.Add(modelHeader.FileName))
            {
                var TempMesh = new ReassignedMesh();
                TempMesh.MeshName = modelHeader.FileName;
                TempMesh.ShadowModel = Shadow;
                TempMesh.faces = ReturnFixedFaces(modelHeader, SlotMap);
                reassignedMesh.Add(TempMesh);
            }
        }

        public void GenerateBones(int MeshID)
        {
            bones = new List<BoneData>();
            BonePlaced = new List<bool>();
            GlobalSlots = Enumerable.Repeat(-1, SlotCount).ToArray();

            //Body Skeleton
            var BodyModel = Body.ModelList.First(x => ModelPalette(x) == 0);
            for (int i = 0; i < BodyModel.bone.Count; i++)
            {
                GlobalSlots[i] = AddBone(BodyModel.bone[i].boneName, BodyModel.bone[i].BoneParentID, BodyModel.bone[i].Position);
            }

            //Extra Bones, the shadow models have their own copy which can differ
            int NextSlot = ExtraSlotStart;
            AddExtraBones(Head, ref NextSlot, MeshID);
            AddExtraBones(Body, ref NextSlot, MeshID);

            BodySlotMaps = GenerateSlotMaps(Body);
            HeadSlotMaps = GenerateSlotMaps(Head);

            OtherLODBones = new Dictionary<(string, string), Vector3>();
            AddOtherLODBones(Head, MeshID);
            AddOtherLODBones(Body, MeshID);
        }

        public void AddOtherLODBones(SSXMPFModelHandler modelHandler, int MeshID)
        {
            for (int i = 0; i < modelHandler.ModelList.Count; i++)
            {
                var Model = modelHandler.ModelList[i];
                if (ModelPalette(Model) != 2 || InMeshLOD(Model, MeshID))
                {
                    continue;
                }
                for (int a = 0; a < Model.bone.Count; a++)
                {
                    var Bone = Model.bone[a];
                    string ParentName = Bone.BoneParentFileID == 0 ? bones[Bone.BoneParentID].BoneName : Model.bone[Bone.BoneParentID].boneName;
                    OtherLODBones.TryAdd((Bone.boneName, ParentName), Bone.Position);
                }
            }
        }

        public int AddBone(string Name, int ParentBone, Vector3 Position)
        {
            BoneData boneData = new BoneData();
            boneData.BoneName = Name;
            boneData.ParentBone = ParentBone;
            boneData.Position = Position;
            boneData.Rotation = Quaternion.Identity;
            boneData.PoseRotation = Quaternion.Identity;
            bones.Add(boneData);
            BonePlaced.Add(false);
            return bones.Count - 1;
        }

        public void AddExtraBones(SSXMPFModelHandler modelHandler, ref int NextSlot, int MeshID)
        {
            var AddedBones = new Dictionary<string, int>();
            for (int i = 0; i < modelHandler.ModelList.Count; i++)
            {
                var Model = modelHandler.ModelList[i];
                if (ModelPalette(Model) != 2 || !InMeshLOD(Model, MeshID))
                {
                    continue;
                }

                for (int a = 0; a < Model.bone.Count; a++)
                {
                    var Bone = Model.bone[a];
                    if (AddedBones.ContainsKey(Bone.boneName))
                    {
                        continue;
                    }

                    //File ID 0 is a body bone, anything else is a bone in this model
                    int Parent = Bone.BoneParentID;
                    if (Bone.BoneParentFileID != 0)
                    {
                        Parent = AddedBones[Model.bone[Bone.BoneParentID].boneName];
                    }

                    int BoneID = AddBone(Bone.boneName, Parent, Bone.Position);
                    AddedBones.Add(Bone.boneName, BoneID);
                    if (NextSlot < SlotCount)
                    {
                        GlobalSlots[NextSlot] = BoneID;
                    }
                    NextSlot++;
                }
            }
        }

        public List<int[]> GenerateSlotMaps(SSXMPFModelHandler modelHandler)
        {
            var SlotMaps = new List<int[]>();
            var FaceBones = new Dictionary<string, int>();
            for (int i = 0; i < modelHandler.ModelList.Count; i++)
            {
                var Model = modelHandler.ModelList[i];
                int[] SlotMap = (int[])GlobalSlots.Clone();

                if (ModelPalette(Model) == 1)
                {
                    //Own bones first, rest gets filled in by IdentifyFaceSlots
                    SlotMap = Enumerable.Repeat(-1, SlotCount).ToArray();
                    for (int a = 0; a < Model.bone.Count; a++)
                    {
                        var Bone = Model.bone[a];
                        if (!FaceBones.ContainsKey(Bone.boneName))
                        {
                            int Parent = Bone.BoneParentID;
                            if (Bone.BoneParentFileID != 0)
                            {
                                Parent = FaceBones[Model.bone[Bone.BoneParentID].boneName];
                            }
                            FaceBones.Add(Bone.boneName, AddBone(Bone.boneName, Parent, Bone.Position));
                        }
                        SlotMap[a] = FaceBones[Bone.boneName];
                    }
                }
                SlotMaps.Add(SlotMap);
            }
            return SlotMaps;
        }

        public void CorrectBoneRotations(int MeshID)
        {
            //Bind Pose
            //Each bone weighted to a vertex has its own copy of that vertex, so a bone sharing enough vertices
            //with already placed bones can be solved for. The hips have nothing to solve against so they take the Tricky A Pose
            //Only this LODs models are used, the LODs dont always share a bind pose (Macs lo neck is 15 degrees off between 2500 and 1200)
            FallbackRotations = new Dictionary<int, Quaternion>();
            var GuessedBones = new List<int>();
            SetBoneRotation(0, Euler.ToQuaternion(-TrickyRotations.GetValueOrDefault(bones[0].BoneName)));

            //Body Skeleton, face models dont know their slots yet
            var Pairs = new List<BlendPair>();
            for (int i = 0; i < Body.ModelList.Count; i++)
            {
                if (InMeshLOD(Body.ModelList[i], MeshID) && ModelPalette(Body.ModelList[i]) != 1)
                {
                    Pairs.AddRange(MapBlendPairs(GetBlendPairs(Body.ModelList[i]), BodySlotMaps[i]));
                }
            }
            for (int i = 0; i < Head.ModelList.Count; i++)
            {
                if (InMeshLOD(Head.ModelList[i], MeshID) && ModelPalette(Head.ModelList[i]) != 1)
                {
                    Pairs.AddRange(MapBlendPairs(GetBlendPairs(Head.ModelList[i]), HeadSlotMaps[i]));
                }
            }
            SolveBones(Pairs, BodyBoneCount, GuessedBones);

            //Face Bones
            for (int i = 0; i < Head.ModelList.Count; i++)
            {
                if (InMeshLOD(Head.ModelList[i], MeshID) && ModelPalette(Head.ModelList[i]) == 1)
                {
                    IdentifyFaceSlots(Head.ModelList[i], HeadSlotMaps[i]);
                    Pairs.AddRange(MapBlendPairs(GetBlendPairs(Head.ModelList[i]), HeadSlotMaps[i]));
                }
            }

            //Extra Bones
            SolveBones(Pairs, bones.Count, GuessedBones);

            //Bones with nothing to go off, some shadow models have the chain flattened onto the parent which shows where the joints sit
            for (int i = 0; i < GuessedBones.Count; i++)
            {
                SolveFromOtherLOD(GuessedBones[i]);
            }

            //Pose for the glTF, the body goes into the Tricky A Pose and everything else keeps its bind rotation so it follows along
            for (int i = 0; i < bones.Count; i++)
            {
                var TempBone = bones[i];
                TempBone.PoseRotation = TempBone.Rotation;
                if (i < BodyBoneCount && TrickyRotations.TryGetValue(TempBone.BoneName, out Vector3 Radians))
                {
                    TempBone.PoseRotation = Euler.ToQuaternion(-Radians);
                }
                bones[i] = TempBone;
            }
        }

        //Places the first Count bones, bones that cant be solved fall back to the reference LOD, known rotations, the face rotation or the Tricky A Pose
        public void SolveBones(List<BlendPair> Pairs, int Count, List<int> GuessedBones)
        {
            while (BonePlaced.Take(Count).Contains(false))
            {
                bool Solved = false;
                for (int i = 0; i < Count; i++)
                {
                    if (BonePlaced[i] || !BonePlaced[bones[i].ParentBone])
                    {
                        continue;
                    }

                    List<Vector3> Local = new List<Vector3>();
                    List<Vector3> World = new List<Vector3>();
                    for (int a = 0; a < Pairs.Count; a++)
                    {
                        if (Pairs[a].SlotA == i && Pairs[a].SlotB != -1 && BonePlaced[Pairs[a].SlotB])
                        {
                            Local.Add(Pairs[a].PositionA);
                            World.Add(Vector3.Transform(Pairs[a].PositionB, bones[Pairs[a].SlotB].WorldMatrix));
                        }
                    }

                    if (SolveRigidTransform(Local, World, out Matrix4x4 BoneWorld, out _))
                    {
                        Matrix4x4.Invert(bones[bones[i].ParentBone].WorldMatrix, out Matrix4x4 ParentInverse);
                        SetBoneRotation(i, Quaternion.CreateFromRotationMatrix(BoneWorld * ParentInverse));
                        Solved = true;
                    }
                }

                if (!Solved)
                {
                    int i = Enumerable.Range(0, Count).First(x => !BonePlaced[x] && BonePlaced[bones[x].ParentBone]);
                    Quaternion Rotation = Quaternion.Identity;
                    if (ReferenceRotations.TryGetValue(bones[i].BoneName, out Quaternion WorldRotation))
                    {
                        Matrix4x4.Invert(bones[bones[i].ParentBone].WorldMatrix, out Matrix4x4 ParentInverse);
                        Rotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateFromQuaternion(WorldRotation) * ParentInverse);
                    }
                    else if (KnownRotations.TryGetValue(bones[i].BoneName, out Quaternion KnownRotation))
                    {
                        Rotation = KnownRotation;
                    }
                    else if (FallbackRotations.TryGetValue(i, out Quaternion FallbackRotation))
                    {
                        Rotation = FallbackRotation;
                    }
                    else if (TrickyRotations.TryGetValue(bones[i].BoneName, out Vector3 Radians))
                    {
                        Rotation = Euler.ToQuaternion(-Radians);
                    }
                    else
                    {
                        GuessedBones.Add(i);
                    }
                    SetBoneRotation(i, Rotation);
                }
            }
        }

        public void SolveFromOtherLOD(int BoneID)
        {
            int Parent = bones[BoneID].ParentBone;
            Matrix4x4.Invert(bones[BoneID].WorldMatrix, out Matrix4x4 BoneInverse);

            //Joint positions local to the bone and local to the parent
            List<Vector3> From = new List<Vector3> { Vector3.Zero };
            List<Vector3> To = new List<Vector3> { bones[BoneID].Position };
            for (int i = 0; i < bones.Count; i++)
            {
                if (i != BoneID && IsChildOf(i, BoneID) && OtherLODBones.TryGetValue((bones[i].BoneName, bones[Parent].BoneName), out Vector3 Other))
                {
                    From.Add(Vector3.Transform(bones[i].WorldMatrix.Translation, BoneInverse));
                    To.Add(Other);
                }
            }

            if (SolveRigidTransform(From, To, out Matrix4x4 Transform, out _, 3))
            {
                SetBoneRotation(BoneID, Quaternion.CreateFromRotationMatrix(Transform));
            }
        }

        public bool IsChildOf(int BoneID, int ParentID)
        {
            while (BoneID != -1)
            {
                if (BoneID == ParentID)
                {
                    return true;
                }
                BoneID = bones[BoneID].ParentBone;
            }
            return false;
        }

        public void IdentifyFaceSlots(SSXMPFModelHandler.MPFModelHeader modelHeader, int[] SlotMap)
        {
            int OwnCount = modelHeader.BoneCount;
            var Pairs = GroupBlendPairs(GetBlendPairs(modelHeader));

            //Parent Slot - Face bones sit on it at their file offset
            int HeadSlot = -1;
            int HeadBone = -1;
            int BestCount = 0;
            for (int s = OwnCount; s < SlotCount; s++)
            {
                int Count = 0;
                for (int f = 0; f < OwnCount; f++)
                {
                    if (modelHeader.bone[f].BoneParentFileID == 0 && Pairs.TryGetValue((s, f), out var Pair) &&
                        SolveRigidTransform(Pair.Item2, Pair.Item1, out Matrix4x4 Transform, out _) &&
                        Vector3.Distance(Transform.Translation, modelHeader.bone[f].Position) < 0.05f)
                    {
                        Count++;
                    }
                }
                if (Count > BestCount)
                {
                    BestCount = Count;
                    HeadSlot = s;
                    HeadBone = modelHeader.bone[0].BoneParentID;
                }
            }
            if (HeadSlot == -1)
            {
                return;
            }
            SlotMap[HeadSlot] = HeadBone;

            //Other Slots - Match against the A Pose offset from a known slot
            bool Found = true;
            while (Found)
            {
                Found = false;
                for (int u = OwnCount; u < SlotCount; u++)
                {
                    if (SlotMap[u] != -1)
                    {
                        continue;
                    }
                    for (int k = OwnCount; k < SlotCount && SlotMap[u] == -1; k++)
                    {
                        if (SlotMap[k] == -1 || !Pairs.TryGetValue((k, u), out var Pair) ||
                            !SolveRigidTransform(Pair.Item2, Pair.Item1, out Matrix4x4 Transform, out _))
                        {
                            continue;
                        }

                        Matrix4x4.Invert(bones[SlotMap[k]].WorldMatrix, out Matrix4x4 KnownInverse);
                        float BestError = float.MaxValue;
                        for (int b = 0; b < BodyBoneCount; b++)
                        {
                            if (SlotMap.Contains(b))
                            {
                                continue;
                            }
                            Matrix4x4 Expected = bones[b].WorldMatrix * KnownInverse;
                            float Error = Vector3.Distance(Expected.Translation, Transform.Translation);
                            if (Error < 1f && RotationAngle(Expected, Transform) < 0.35f && Error < BestError)
                            {
                                BestError = Error;
                                SlotMap[u] = b;
                                Found = true;
                            }
                        }
                    }
                }
            }

            //Face bones use their own solve when they share enough vertices with the parent,
            //the rest use the rotation of the one with the most shared vertices (they mostly match)
            int BestPoints = 0;
            Quaternion FaceRotation = Quaternion.Identity;
            var FaceRotations = new Dictionary<int, Quaternion>();
            for (int f = 0; f < OwnCount; f++)
            {
                if (modelHeader.bone[f].BoneParentFileID == 0 && Pairs.TryGetValue((HeadSlot, f), out var Pair) &&
                    SolveRigidTransform(Pair.Item2, Pair.Item1, out Matrix4x4 Transform, out _) &&
                    Vector3.Distance(Transform.Translation, modelHeader.bone[f].Position) < 0.05f)
                {
                    FaceRotations.Add(f, Quaternion.CreateFromRotationMatrix(Transform));
                    if (Pair.Item1.Count > BestPoints)
                    {
                        BestPoints = Pair.Item1.Count;
                        FaceRotation = FaceRotations[f];
                    }
                }
            }

            //Rest get solved against the other face bones later
            for (int f = 0; f < OwnCount; f++)
            {
                if (FaceRotations.TryGetValue(f, out Quaternion Rotation))
                {
                    SetBoneRotation(SlotMap[f], Rotation);
                }
                else
                {
                    FallbackRotations[SlotMap[f]] = FaceRotation;
                }
            }
        }

        public void SetBoneRotation(int BoneID, Quaternion Rotation)
        {
            var TempBone = bones[BoneID];
            TempBone.Rotation = Quaternion.Normalize(Rotation);
            bones[BoneID] = TempBone;
            BonePlaced[BoneID] = true;
            UpdateWorldMatrices();
        }

        public void UpdateWorldMatrices()
        {
            //Parents are always before their children
            for (int i = 0; i < bones.Count; i++)
            {
                var TempBone = bones[i];
                TempBone.WorldMatrix = Matrix4x4.CreateFromQuaternion(TempBone.Rotation) * Matrix4x4.CreateTranslation(TempBone.Position);
                if (TempBone.ParentBone != -1)
                {
                    TempBone.WorldMatrix *= bones[TempBone.ParentBone].WorldMatrix;
                }
                bones[i] = TempBone;
            }
        }

        public List<BlendPair> GetBlendPairs(SSXMPFModelHandler.MPFModelHeader modelHeader)
        {
            var Pairs = new List<BlendPair>();
            for (int i = 0; i < modelHeader.MaterialGroups.Count; i++)
            {
                for (int a = 0; a < modelHeader.MaterialGroups[i].flexableMesh.Count; a++)
                {
                    var WeightedVertices = modelHeader.MaterialGroups[i].flexableMesh[a].weightedVertices;
                    for (int b = 0; b < WeightedVertices.Count; b++)
                    {
                        var Weights = WeightedVertices[b].boneWeights;
                        for (int c = 0; c < Weights.Count; c++)
                        {
                            for (int d = 0; d < Weights.Count; d++)
                            {
                                if (Weights[c].BoneID != Weights[d].BoneID)
                                {
                                    BlendPair blendPair = new BlendPair();
                                    blendPair.SlotA = Weights[c].BoneID;
                                    blendPair.PositionA = Weights[c].Position;
                                    blendPair.SlotB = Weights[d].BoneID;
                                    blendPair.PositionB = Weights[d].Position;
                                    Pairs.Add(blendPair);
                                }
                            }
                        }
                    }
                }
            }
            return Pairs;
        }

        public List<BlendPair> MapBlendPairs(List<BlendPair> Pairs, int[] SlotMap)
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                var TempPair = Pairs[i];
                TempPair.SlotA = SlotMap[TempPair.SlotA];
                TempPair.SlotB = SlotMap[TempPair.SlotB];
                Pairs[i] = TempPair;
            }
            return Pairs;
        }

        //(Slot A, Slot B) -> (Positions local to A, Positions local to B)
        public Dictionary<(int, int), (List<Vector3>, List<Vector3>)> GroupBlendPairs(List<BlendPair> Pairs)
        {
            var Groups = new Dictionary<(int, int), (List<Vector3>, List<Vector3>)>();
            for (int i = 0; i < Pairs.Count; i++)
            {
                if (!Groups.TryGetValue((Pairs[i].SlotA, Pairs[i].SlotB), out var Group))
                {
                    Group = (new List<Vector3>(), new List<Vector3>());
                    Groups.Add((Pairs[i].SlotA, Pairs[i].SlotB), Group);
                }
                Group.Item1.Add(Pairs[i].PositionA);
                Group.Item2.Add(Pairs[i].PositionB);
            }
            return Groups;
        }

        public List<ReassignedFace> ReturnFixedFaces(SSXMPFModelHandler.MPFModelHeader modelHeader, int[] SlotMap, bool MergeMaterials = true)
        {
            List<ReassignedFace> NewFaces = new List<ReassignedFace>();

            for (int i = 0; i < modelHeader.MaterialGroups.Count; i++)
            {
                var MaterialGroup = modelHeader.MaterialGroups[i];
                int MatId = MergeMaterials ? GetMaterialID(modelHeader.materialDataList[MaterialGroup.MaterialID]) : MaterialGroup.MaterialID;

                for (int a = 0; a < MaterialGroup.staticMesh.Count; a++)
                {
                    var Data = MaterialGroup.staticMesh[a];
                    int BoneID = MapSlot(SlotMap, Data.BoneAssigment);
                    Matrix4x4 matrix4X4 = bones[BoneID].WorldMatrix;
                    var Weight = SingleWeight(BoneID);

                    for (int b = 0; b < Data.faces.Count; b++)
                    {
                        var Face = Data.faces[b];
                        ReassignedFace NewFace = new ReassignedFace();
                        NewFace.V1 = Vector3.Transform(Face.V1, matrix4X4);
                        NewFace.V2 = Vector3.Transform(Face.V2, matrix4X4);
                        NewFace.V3 = Vector3.Transform(Face.V3, matrix4X4);

                        NewFace.Normal1 = TransformNormal(Face.Normal1, matrix4X4);
                        NewFace.Normal2 = TransformNormal(Face.Normal2, matrix4X4);
                        NewFace.Normal3 = TransformNormal(Face.Normal3, matrix4X4);

                        NewFace.UV1 = Face.UV1;
                        NewFace.UV2 = Face.UV2;
                        NewFace.UV3 = Face.UV3;

                        NewFace.Weight1 = Weight;
                        NewFace.Weight2 = Weight;
                        NewFace.Weight3 = Weight;
                        NewFace.MaterialID = MatId;

                        if ((NewFace.V1 != NewFace.V2) && (NewFace.V2 != NewFace.V3) && (NewFace.V3 != NewFace.V1))
                        {
                            NewFaces.Add(NewFace);
                        }
                    }
                }

                for (int a = 0; a < MaterialGroup.flexableMesh.Count; a++)
                {
                    var Data = MaterialGroup.flexableMesh[a];
                    for (int b = 0; b < Data.faces.Count; b++)
                    {
                        var Face = Data.faces[b];
                        ReassignedFace NewFace = new ReassignedFace();
                        (NewFace.V1, NewFace.Normal1, NewFace.Weight1) = FixFlexVertex(Face.V1, SlotMap);
                        (NewFace.V2, NewFace.Normal2, NewFace.Weight2) = FixFlexVertex(Face.V2, SlotMap);
                        (NewFace.V3, NewFace.Normal3, NewFace.Weight3) = FixFlexVertex(Face.V3, SlotMap);

                        NewFace.UV1 = Face.V1.UV;
                        NewFace.UV2 = Face.V2.UV;
                        NewFace.UV3 = Face.V3.UV;
                        NewFace.MaterialID = MatId;

                        if ((NewFace.V1 != NewFace.V2) && (NewFace.V2 != NewFace.V3) && (NewFace.V3 != NewFace.V1))
                        {
                            NewFaces.Add(NewFace);
                        }
                    }
                }
            }

            return NewFaces;
        }

        public (Vector3, Vector3, SSXMPFModelHandler.WeightedVertex) FixFlexVertex(SSXMPFModelHandler.FlexableVertex Vertex, int[] SlotMap)
        {
            //Each bone has its own local copy of the position, blend them together using the weights
            //The weights dont always add up to exactly 1 (32766 out of 32768) so divide through or the vertex gets pulled towards the origin
            Vector3 Position = Vector3.Zero;
            float Total = 0;
            var Weight = new SSXMPFModelHandler.WeightedVertex();
            Weight.boneWeights = new List<SSXMPFModelHandler.BoneWeight>();
            for (int i = 0; i < Vertex.boneWeights.Count; i++)
            {
                var TempWeight = Vertex.boneWeights[i];
                TempWeight.BoneID = MapSlot(SlotMap, TempWeight.BoneID);
                Position += Vector3.Transform(TempWeight.Position, bones[TempWeight.BoneID].WorldMatrix) * TempWeight.Weight;
                Total += TempWeight.Weight;
                Weight.boneWeights.Add(TempWeight);
            }
            if (Total > 0)
            {
                Position /= Total;
            }

            Vector3 Normal = TransformNormal(Vertex.Normal, bones[MapSlot(SlotMap, Vertex.NormalBoneID)].WorldMatrix);

            return (Position, Normal, Weight);
        }

        public int MapSlot(int[] SlotMap, int Slot)
        {
            //Unidentified slots fall back to the hips
            if (Slot < 0 || Slot >= SlotMap.Length || SlotMap[Slot] == -1)
            {
                return 0;
            }
            return SlotMap[Slot];
        }

        public SSXMPFModelHandler.WeightedVertex SingleWeight(int BoneID)
        {
            var Weight = new SSXMPFModelHandler.WeightedVertex();
            Weight.boneWeights = new List<SSXMPFModelHandler.BoneWeight>();
            var boneWeight = new SSXMPFModelHandler.BoneWeight();
            boneWeight.BoneID = BoneID;
            boneWeight.Weight = 1f;
            Weight.boneWeights.Add(boneWeight);
            return Weight;
        }

        public Vector3 TransformNormal(Vector3 Normal, Matrix4x4 matrix4X4)
        {
            if (Normal == Vector3.Zero)
            {
                return Normal;
            }
            return Vector3.Normalize(Vector3.TransformNormal(Normal, matrix4X4));
        }

        public int GetMaterialID(SSXMPFModelHandler.MaterialData materialData)
        {
            //Same name can show up with different values (two face entries), only merge exact matches
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i].Name == materialData.Name && materials[i].X == materialData.X && materials[i].Y == materialData.Y && materials[i].Z == materialData.Z)
                {
                    return i;
                }
            }
            materials.Add(materialData);
            return materials.Count - 1;
        }

        //Finds the rigid transform moving From onto To (Horn's quaternion method)
        //Fails if there isnt enough spread in the points to fix the rotation
        public static bool SolveRigidTransform(List<Vector3> From, List<Vector3> To, out Matrix4x4 Transform, out float Residual, int MinPoints = 4)
        {
            Transform = Matrix4x4.Identity;
            Residual = 0;
            if (From.Count < MinPoints)
            {
                return false;
            }

            Vector3 FromCenter = Vector3.Zero;
            Vector3 ToCenter = Vector3.Zero;
            for (int i = 0; i < From.Count; i++)
            {
                FromCenter += From[i];
                ToCenter += To[i];
            }
            FromCenter /= From.Count;
            ToCenter /= From.Count;

            double[,] S = new double[3, 3];
            double[,] Spread = new double[3, 3];
            for (int i = 0; i < From.Count; i++)
            {
                Vector3 A = From[i] - FromCenter;
                Vector3 B = To[i] - ToCenter;
                float[] a = { A.X, A.Y, A.Z };
                float[] b = { B.X, B.Y, B.Z };
                for (int x = 0; x < 3; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        S[x, y] += a[x] * b[y];
                        Spread[x, y] += a[x] * a[y];
                    }
                }
            }

            //Points in a line leave the rotation around that line free
            JacobiEigen(Spread, out double[] SpreadValues, out _);
            Array.Sort(SpreadValues);
            if (SpreadValues[2] <= 1e-8 || SpreadValues[1] < SpreadValues[2] * 1e-4)
            {
                return false;
            }

            double[,] N = new double[4, 4]
            {
                { S[0,0] + S[1,1] + S[2,2], S[1,2] - S[2,1], S[2,0] - S[0,2], S[0,1] - S[1,0] },
                { S[1,2] - S[2,1], S[0,0] - S[1,1] - S[2,2], S[0,1] + S[1,0], S[2,0] + S[0,2] },
                { S[2,0] - S[0,2], S[0,1] + S[1,0], -S[0,0] + S[1,1] - S[2,2], S[1,2] + S[2,1] },
                { S[0,1] - S[1,0], S[2,0] + S[0,2], S[1,2] + S[2,1], -S[0,0] - S[1,1] + S[2,2] },
            };
            JacobiEigen(N, out double[] Values, out double[,] Vectors);
            int Max = 0;
            for (int i = 1; i < 4; i++)
            {
                if (Values[i] > Values[Max])
                {
                    Max = i;
                }
            }

            Quaternion Rotation = Quaternion.Normalize(new Quaternion((float)Vectors[1, Max], (float)Vectors[2, Max], (float)Vectors[3, Max], (float)Vectors[0, Max]));
            Vector3 Translation = ToCenter - Vector3.Transform(FromCenter, Rotation);
            Transform = Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Translation);

            for (int i = 0; i < From.Count; i++)
            {
                Residual += Vector3.DistanceSquared(Vector3.Transform(From[i], Transform), To[i]);
            }
            Residual = MathF.Sqrt(Residual / From.Count);
            return true;
        }

        //Eigen values and vectors (columns) of a symmetric matrix
        public static void JacobiEigen(double[,] Matrix, out double[] Values, out double[,] Vectors)
        {
            int n = Matrix.GetLength(0);
            double[,] A = (double[,])Matrix.Clone();
            Vectors = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                Vectors[i, i] = 1;
            }

            for (int Sweep = 0; Sweep < 60; Sweep++)
            {
                double Off = 0;
                double Diagonal = 0;
                for (int i = 0; i < n; i++)
                {
                    Diagonal += A[i, i] * A[i, i];
                    for (int j = 0; j < n; j++)
                    {
                        if (i != j)
                        {
                            Off += A[i, j] * A[i, j];
                        }
                    }
                }
                if (Off <= 1e-24 * Diagonal)
                {
                    break;
                }

                for (int p = 0; p < n; p++)
                {
                    for (int q = p + 1; q < n; q++)
                    {
                        if (Math.Abs(A[p, q]) < 1e-30)
                        {
                            continue;
                        }
                        double Theta = (A[q, q] - A[p, p]) / (2 * A[p, q]);
                        double t = (Theta >= 0 ? 1 : -1) / (Math.Abs(Theta) + Math.Sqrt(Theta * Theta + 1));
                        double c = 1 / Math.Sqrt(t * t + 1);
                        double s = t * c;
                        for (int k = 0; k < n; k++)
                        {
                            double akp = A[k, p];
                            double akq = A[k, q];
                            A[k, p] = c * akp - s * akq;
                            A[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double apk = A[p, k];
                            double aqk = A[q, k];
                            A[p, k] = c * apk - s * aqk;
                            A[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double vkp = Vectors[k, p];
                            double vkq = Vectors[k, q];
                            Vectors[k, p] = c * vkp - s * vkq;
                            Vectors[k, q] = s * vkp + c * vkq;
                        }
                    }
                }
            }

            Values = new double[n];
            for (int i = 0; i < n; i++)
            {
                Values[i] = A[i, i];
            }
        }

        public static float RotationAngle(Matrix4x4 A, Matrix4x4 B)
        {
            Quaternion QA = Quaternion.CreateFromRotationMatrix(A);
            Quaternion QB = Quaternion.CreateFromRotationMatrix(B);
            float Dot = MathF.Min(1f, MathF.Abs(Quaternion.Dot(Quaternion.Normalize(QA), Quaternion.Normalize(QB))));
            return 2f * MathF.Acos(Dot);
        }

        public struct BoneData
        {
            public string BoneName;
            public int ParentBone;
            public Vector3 Position;
            public Quaternion Rotation; //Bind pose, what the mesh data is in
            public Matrix4x4 WorldMatrix; //Bind pose
            public Quaternion PoseRotation; //What the glTF bones show
        }

        public struct BlendPair
        {
            public int SlotA;
            public Vector3 PositionA; //Local to SlotA
            public int SlotB;
            public Vector3 PositionB; //Local to SlotB
        }

        public struct ReassignedMesh
        {
            public string MeshName;
            public bool ShadowModel;
            public List<Vector3> IKPoints;
            public List<ReassignedFace> faces;
        }

        public struct ReassignedFace
        {
            public Vector3 V1;
            public Vector3 V2;
            public Vector3 V3;

            public Vector3 Normal1;
            public Vector3 Normal2;
            public Vector3 Normal3;

            public Vector2 UV1;
            public Vector2 UV2;
            public Vector2 UV3;

            public SSXMPFModelHandler.WeightedVertex Weight1;
            public SSXMPFModelHandler.WeightedVertex Weight2;
            public SSXMPFModelHandler.WeightedVertex Weight3;

            public int MaterialID;
        }
    }
}
