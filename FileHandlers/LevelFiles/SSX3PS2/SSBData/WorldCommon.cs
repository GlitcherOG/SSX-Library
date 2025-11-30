using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SSXLibrary.Utilities;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData
{
    public class WorldCommon
    {
        public static ObjectID ObjectIDLoad(Stream stream)
        {
            ObjectID U = new ObjectID();

            U.TrackID = StreamUtil.ReadUInt8(stream);
            U.RID = StreamUtil.ReadInt24(stream);

            return U;
        }
    }
    public struct ObjectID
    {
        public int TrackID;
        public int RID;
    }

}
