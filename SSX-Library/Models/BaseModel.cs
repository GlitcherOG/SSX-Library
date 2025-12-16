using System.Numerics;

namespace SSX_Library.Models
{
    public class BaseModel
    {
        public string name = "";

        public bool shadowModel;
        public int morphCount;

        public List<Material> materials = new List<Material>();
        public List<Bones> bones = new List<Bones>();
        public List<Vector3> iKPoints = new List<Vector3>();
        public List<Face> faces = new List<Face>();

        public string ValidFile()
        {
            //Check Bones
            for (int i = 0; i < bones.Count; i++)
            {
                var Bone1 = bones[i];

                if (Bone1.ParentID != -1)
                {
                    bool Test = false;
                    for (int j = 0; j < bones.Count; j++)
                    {
                        var Bone2 = bones[j];

                        if(Bone1.ParentID == Bone2.ID && Bone1.ParentFileID == Bone2.FileID)
                        {
                            Test = true;
                            break;
                        }    
                    }

                    if (!Test)
                    {
                        return "Missing Missing Bone and FileID " + Bone1.ParentID + "," + Bone1.ParentFileID;
                    }
                }
            }

            //Check Weight
            for (int i = 0; i < faces.Count; i++)
            {
                var WeightCheck = CheckWeights(faces[i].Weight1);

                if (!WeightCheck.Item1)
                {
                    return WeightCheck.Item2;
                }

                WeightCheck = CheckWeights(faces[i].Weight2);

                if (!WeightCheck.Item1)
                {
                    return WeightCheck.Item2;
                }

                WeightCheck = CheckWeights(faces[i].Weight3);

                if (!WeightCheck.Item1)
                {
                    return WeightCheck.Item2;
                }

            }

            return "Valid";
        }

        private (bool,string) CheckWeights(List<BoneWeight> weights)
        {
            for (int i = 0; i < weights.Count; i++)
            {
                bool Test = false;
                var weight = weights[i];
                for (int a = 0; a < bones.Count; a++)
                {
                    if (bones[a].FileID == weight.FileID && bones[a].ID == weight.BoneID)
                    {
                        Test = true;
                        break;
                    }
                }

                if(!Test)
                {
                    return (false, "Missing Missing Bone and FileID " + weight.BoneID +","+weight.FileID);
                }
            }

            return (true,"");
        }

        public struct Bones
        {
            public int FileID;
            public int ID;

            public int ParentFileID;
            public int ParentID;

            public string Name;
            public Vector3 Position;
            public Quaternion Quaternion;
        }

        public struct Material
        {
            public string MainTexture;
            public string Texture1;
            public string Texture2;
            public string Texture3;
            public string Texture4;

            public float FactorFloat;
            public float Unused1Float;
            public float Unused2Float;
        }

        public struct BoneWeight
        {
            public int Weight;
            public int BoneID;
            public int FileID;
        }

        public struct Face
        {
            public Vector3 V1;
            public Vector3 V2;
            public Vector3 V3;

            public Vector2 UV1;
            public Vector2 UV2;
            public Vector2 UV3;

            public Vector3 Normal1;
            public Vector3 Normal2;
            public Vector3 Normal3;

            public List<BoneWeight> Weight1;
            public List<BoneWeight> Weight2;
            public List<BoneWeight> Weight3;

            public List<Vector3> MorphPoint1;
            public List<Vector3> MorphPoint2;
            public List<Vector3> MorphPoint3;

            public int MaterialID;
        }
    }
}
