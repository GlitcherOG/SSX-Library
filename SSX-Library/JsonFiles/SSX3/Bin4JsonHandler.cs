using Newtonsoft.Json;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.JsonFiles.SSX3
{
    public class Bin4JsonHandler
    {
        public List<Bin4> bin4s = new List<Bin4>();

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

        public static Bin4JsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<Bin4JsonHandler>(stream);
                return container;
            }
            else
            {
                return new Bin4JsonHandler();
            }
        }

        public struct Bin4
        {
            public string Name;

            public ObjectID objectID;

            public List<PointData> PointDatas;

        }

        public struct PointData
        {
            public float[] UVector1;
            public float[] UVector2;

            public float U0;
        }
    }
}
