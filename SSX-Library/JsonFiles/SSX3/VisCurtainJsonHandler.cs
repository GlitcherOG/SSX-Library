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
    public class VisCurtainJsonHandler
    {
        public List<VisCurtain> VisCurtains = new List<VisCurtain>();

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

        public static VisCurtainJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<VisCurtainJsonHandler>(stream);
                return container;
            }
            else
            {
                return new VisCurtainJsonHandler();
            }
        }


        public struct VisCurtain
        {
            public float[] BoundSphere;

            public float[] Corner0;
            public float[] Corner1;
            public float[] Corner2;
            public float[] Corner3;

            public float[] PlaneNormal;
            public float PlaneDistance;

            public float[] BBoxMin;
            public float[] BBoxMax;

            public int Flag;
        }
    }
}
