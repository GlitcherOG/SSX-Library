using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.Models
{
    public class BaseModel
    {
        public string name = "";

        public List<Material> materials = new List<Material>();
        public List<Bones> bones = new List<Bones>();
        public List<Vector3> iKPoints = new List<Vector3>();
        public List<Face> faces = new List<Face>();

        public struct Bones
        {
            public int ID;
            public int ParentID;
            public string Name;
            public Vector3 Position;
            public Vector3 Radians;

            public int FileID;
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

        public struct BoneWeightList
        {
            public List<BoneWeight> boneWeights;
        }

        public struct BoneWeight
        {
            public int Weight;
            public int BoneID;
            public int FileID;

            public string boneName;
        }

        public struct Face
        {
            public Vector3 V1;
            public Vector3 V2;
            public Vector3 V3;

            public Vector4 UV1;
            public Vector4 UV2;
            public Vector4 UV3;

            public Vector3 Normal1;
            public Vector3 Normal2;
            public Vector3 Normal3;

            public BoneWeightList Weight1;
            public BoneWeightList Weight2;
            public BoneWeightList Weight3;

            public List<Vector3> MorphPoint1;
            public List<Vector3> MorphPoint2;
            public List<Vector3> MorphPoint3;

            public int MaterialID;
        }

    }
}
