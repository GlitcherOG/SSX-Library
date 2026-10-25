using Newtonsoft.Json;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data;
using System.IO;


namespace SSXLibrary.JsonFiles.SSX3
{
    public class InstanceJsonHandler
    {
        public List<Instance> Instances = new List<Instance>();

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

        public static InstanceJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<InstanceJsonHandler>(stream);
                return container;
            }
            else
            {
                return new InstanceJsonHandler();
            }
        }


        public struct Instance
        {
            public string Name;

            public ObjectID objectID;

            public int U0;
            public int U1;
            public int U2;
            public int U3;

            public float[] Position;
            public float[] Rotation;
            public float[] Scale;

            public int U4;

            public ObjectID Model;
            public int U6;
            public int U7;

            public int U8;
            public int U9;
            public int U10;
            public int U11;
            public int U12;

            // Per-model-vertex baked lighting decoded from the instance tail: one
            // raw 16-bit ABGR1555 colour per model vertex, PS2 vertex-stream order.
            // Only populated when WorldInstance.EmitVertexColors is set (large).
            public List<int> VertexColors;
        }
    }
}
