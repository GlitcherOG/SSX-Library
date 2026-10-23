//WARNING - Claude assisted code
//This combiner (and the flex mesh reading in SSXMPFModelHandler) was written with help from Claude.
//It has been checked against every character in CHAR HI, but give it a bit more suspicion than the rest of the library.
//
//Why it wasnt done by hand, the SSX 2000 model format is a pain to work with:
// - Bones only store a name, parent and offset. No rotations at all, so the bind pose has to be rebuilt
//   from the Tricky skeleton and by solving it back out of the mesh data.
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

        public void StartReassignMesh(int MeshID)
        {
            if (MeshID != -1)
            {
                if (BodyBool)
                {
                    if (Head != null && Body != null)
                    {
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
                CorrectBoneRotations();
                for (int i = 0; i < bones.Count; i++)
                {
                    ReferenceRotations[bones[i].BoneName] = Quaternion.CreateFromRotationMatrix(bones[i].WorldMatrix);
                }
            }

            GenerateBones(MeshID);
            CorrectBoneRotations();

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

        public void CorrectBoneRotations()
        {
            //Body Skeleton to the Tricky A Pose
            for (int i = 0; i < BodyBoneCount; i++)
            {
                var TempBone = bones[i];
                if (TrickyRotations.TryGetValue(TempBone.BoneName, out Vector3 Radians))
                {
                    TempBone.Rotation = Euler.ToQuaternion(-Radians);
                }
                bones[i] = TempBone;
                BonePlaced[i] = true;
            }
            UpdateWorldMatrices();

            //Face Bones
            FallbackRotations = new Dictionary<int, Quaternion>();
            var GuessedBones = new List<int>();
            for (int i = 0; i < Head.ModelList.Count; i++)
            {
                if (ModelPalette(Head.ModelList[i]) == 1)
                {
                    IdentifyFaceSlots(Head.ModelList[i], HeadSlotMaps[i]);
                }
            }

            //Extra Bones
            //Each bone weighted to a vertex has its own copy of that vertex, so a bone sharing enough vertices
            //with already placed bones can be solved for. Bones that cant be solved fall back to the Tricky table or no rotation
            var Pairs = new List<BlendPair>();
            for (int i = 0; i < Body.ModelList.Count; i++)
            {
                Pairs.AddRange(MapBlendPairs(GetBlendPairs(Body.ModelList[i]), BodySlotMaps[i]));
            }
            for (int i = 0; i < Head.ModelList.Count; i++)
            {
                Pairs.AddRange(MapBlendPairs(GetBlendPairs(Head.ModelList[i]), HeadSlotMaps[i]));
            }

            while (BonePlaced.Contains(false))
            {
                bool Solved = false;
                for (int i = 0; i < bones.Count; i++)
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
                    int i = Enumerable.Range(0, bones.Count).First(x => !BonePlaced[x] && BonePlaced[bones[x].ParentBone]);
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

            //Bones with nothing to go off, some shadow models have the chain flattened onto the parent which shows where the joints sit
            for (int i = 0; i < GuessedBones.Count; i++)
            {
                SolveFromOtherLOD(GuessedBones[i]);
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
            Vector3 Position = Vector3.Zero;
            var Weight = new SSXMPFModelHandler.WeightedVertex();
            Weight.boneWeights = new List<SSXMPFModelHandler.BoneWeight>();
            for (int i = 0; i < Vertex.boneWeights.Count; i++)
            {
                var TempWeight = Vertex.boneWeights[i];
                TempWeight.BoneID = MapSlot(SlotMap, TempWeight.BoneID);
                Position += Vector3.Transform(TempWeight.Position, bones[TempWeight.BoneID].WorldMatrix) * TempWeight.Weight;
                Weight.boneWeights.Add(TempWeight);
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
            public Quaternion Rotation;
            public Matrix4x4 WorldMatrix;
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
