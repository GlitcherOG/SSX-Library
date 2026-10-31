using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SSXLibrary.JsonFiles.Tricky
{
    [Serializable]
    public class LightJsonHandler
    {
        public List<LightJson> Lights = new List<LightJson>();

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

        public static LightJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<LightJsonHandler>(stream);
                return container;
            }
            else
            {
                return new LightJsonHandler();
            }
        }

        [Serializable]
        public struct LightJson
        {
            public string LightName;

            public int Type; //TrickyLightType
            public int SpriteRes;
            public float Brightness;
            public int UnknownInt1;
            public float[] Colour;
            public float[] Direction;
            public float[] Position;
            public float[] LowestXYZ;
            public float[] HighestXYZ;
            public float OuterConeCosine;
            public int UnknownInt2;
            public float UnknownFloat3;
            public int UnknownInt3;

            public int Hash;
            //public bool IncludeSound;
            //public SoundData? Sounds;
        }

        [Serializable]
        public struct SoundData
        {
            public int CollisonSound;
            public List<ExternalSound> ExternalSounds;
        }
        [Serializable]
        public struct ExternalSound
        {
            public int EmitterType;
            public int SoundIndex;
            public float[] Offset;
            public float Radius; //Radius?
            public float U6;
        }
    }
}
