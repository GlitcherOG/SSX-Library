using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.JsonFiles.SSX3
{
    public class ParticleInstanceJsonHandler
    {
        public List<ParticleInstance> ParticleInstances = new List<ParticleInstance>();

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

        public static ParticleInstanceJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<ParticleInstanceJsonHandler>(stream);
                return container;
            }
            else
            {
                return new ParticleInstanceJsonHandler();
            }
        }


        public struct ParticleInstance
        {
            public int[] Reserved0;

            public float[] Position;
            public float[] Rotation;
            public float[] Scale;

            public float[] BoundingSphere;

            public int TrackID;
            public int RID;
            public int DuplicateTrackID;
            public int DuplicateRID;

            public float[] AABBMin;
            public float[] AABBMax;

            public int[] Reserved1;
        }
    }
}
