using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SSXLibrary.JsonFiles.Tricky
{
    public class ModelJsonHandler
    {
        public List<ModelJson> Models = new List<ModelJson>();

        public void CreateJson(string path, bool Inline = false)
        {
            var TempFormating = Formatting.None;
            if (Inline)
            {
                TempFormating = Formatting.Indented;
            }

            var serializer = JsonConvert.SerializeObject(this, TempFormating);
            File.WriteAllText(path, serializer);
        }

        public static ModelJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<ModelJsonHandler>(stream);
                return container;
            }
            else
            {
                return new ModelJsonHandler();
            }
        }

        [Serializable]
        public struct ModelJson
        {
            public string ModelName;
            public int Unknown3;
            public float AnimTime;
            public List<ObjectHeader> ModelObjects;
        }
        [Serializable]
        public struct ObjectHeader
        {
            public string ObjectName;

            public int ParentID;
            public int Flags;

            public ObjectAnimation? Animation;
            public List<MeshHeader> MeshData;

            public float[] Position;
            public float[] Rotation;
            public float[] Scale;

            public bool IncludeAnimation;
            public bool IncludeMatrix;
        }
        [Serializable]
        public struct MeshHeader
        {
            public string MeshPath;
            public int MaterialID;
        }
        [Serializable]
        public struct ObjectAnimation
        {
            public float[] BasePosition;
            public float[] BaseRotation;

            public int AnimationAction;
            public List<AnimationEntry> AnimationEntries;
        }
        [Serializable]
        public struct AnimationEntry
        {
            public List<AnimationMath> AnimationMaths;
        }
        [Serializable]
        public struct AnimationMath
        {
            public float Coefficient1;
            public float Coefficient2;
            public float Coefficient3;
            public float Coefficient4;
            public float StartTime;
            public float EndTime;
        }
    }
}
