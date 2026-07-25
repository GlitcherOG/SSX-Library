using SSX_Library.Internal.Utilities;
using SSX_Library.Internal.Utilities.StreamExtensions;
using System.IO;

namespace SSXLibrary.FileHandlers.LevelFiles.OnTourPS2
{
    public class PSMHandler
    {
        public float U0;
        public float U1;
        public int NumArrays;
        public List<NameList> nameLists = new List<NameList>();

        public void LoadPSM(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                bool test = false;

                U0  = StreamUtil.ReadFloat(stream);
                U1 = StreamUtil.ReadFloat(stream);
                NumArrays = StreamUtil.ReadUInt32(stream);
                nameLists = new List<NameList>();
                for (int i = 0; i < NumArrays; i++)
                {
                    var TempNameList = new NameList();
                    TempNameList.Unknown2 = StreamUtil.ReadUInt32(stream);
                    TempNameList.NumStrings = StreamUtil.ReadUInt32(stream);
                    TempNameList.strings = new List<string>();
                    for (int a = 0; a < TempNameList.NumStrings; a++)
                    {
                        TempNameList.strings.Add(StreamUtil.ReadNullEndString(stream));
                    }
                    stream.AlignBy(4);
                    nameLists.Add(TempNameList);
                }
            }
        }

        public struct NameList
        {
            public int Unknown2;
            public int NumStrings;
            public List<string> strings;
        }
    }
}
