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
    public class Bin7JsonHandler
    {
        public List<Bin7File> bin7Files = new List<Bin7File>();

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

        public static Bin7JsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<Bin7JsonHandler>(stream);
                return container;
            }
            else
            {
                return new Bin7JsonHandler();
            }
        }


        public struct Bin7File
        {
            public int TrackID;
            public int RID;

            public int U0;
            public int U1;
            public int U2;
            public int U3;

            public float[] U4;
            public float[] U5;
            public float[] U6;
            public float[] U7;

            public int U8;
            public int U9;
            public int U10;
            public int U11;
        }
    }
}
