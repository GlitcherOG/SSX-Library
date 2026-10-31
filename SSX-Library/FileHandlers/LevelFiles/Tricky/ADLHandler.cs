using SSXLibrary.Internal.Utilities;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.Tricky
{
    public class ADLHandler
    {
        public byte[] Magic = new byte[4] { 0x00, 0xCC, 0xCC, 0xCC };
        public float U0 = 1f;
        public int UCount;
        public List<HashSound> HashSounds = new List<HashSound>();

        //Put into a seperate list just so its easier to find out wtf is going on
        public List<SoundData> ExternalSoundsList = new List<SoundData>();

        public void Load(string path)
        {
            using (Stream stream = File.Open(path, FileMode.Open))
            {
                Magic = StreamUtil.ReadBytes(stream,4);
                U0 = StreamUtil.ReadFloat(stream);
                UCount = StreamUtil.ReadUInt32(stream);

                HashSounds = new List<HashSound>();

                for (int i = 0; i < UCount; i++)
                {
                    var NewUStruct = new HashSound();

                    NewUStruct.Hash = StreamUtil.ReadUInt32(stream);
                    NewUStruct.SoundDataOffset = StreamUtil.ReadUInt32(stream);

                    NewUStruct.Sound = new SoundData();
                    int TempPos = (int)stream.Position;
                    stream.Position = NewUStruct.SoundDataOffset;

                    //UStruct0
                    NewUStruct.Sound.CollisonSound = StreamUtil.ReadUInt32(stream);
                    NewUStruct.Sound.ExternalSoundsCount = StreamUtil.ReadUInt32(stream);
                    NewUStruct.Sound.ExternalSounds = new List<ExternalSound>();

                    for (int a = 0; a < NewUStruct.Sound.ExternalSoundsCount; a++)
                    {
                        var NewUStruct2 = new ExternalSound();
                        NewUStruct2.EmitterType = StreamUtil.ReadUInt32(stream);
                        NewUStruct2.SoundIndex = StreamUtil.ReadUInt32(stream);
                        NewUStruct2.Offset = StreamUtil.ReadVector3(stream);
                        NewUStruct2.Radius = StreamUtil.ReadFloat(stream);
                        // ExternalSound records are variable-sized. The retail runtime walks them with exactly
                        // these strides: type0=0x1c, type1/2=0x30, type3=0x18. The former fixed seven-field read
                        // truncated rare type1/2 volume records and misaligned every following sibling record.
                        if (NewUStruct2.EmitterType != 3) NewUStruct2.U6 = StreamUtil.ReadFloat(stream);
                        if (NewUStruct2.EmitterType == 1 || NewUStruct2.EmitterType == 2)
                        {
                            NewUStruct2.U7 = StreamUtil.ReadFloat(stream);
                            NewUStruct2.U8 = StreamUtil.ReadFloat(stream);
                            NewUStruct2.U9 = StreamUtil.ReadFloat(stream);
                            NewUStruct2.U10 = StreamUtil.ReadFloat(stream);
                            NewUStruct2.U11 = StreamUtil.ReadFloat(stream);
                        }
                        else if (NewUStruct2.EmitterType != 0 && NewUStruct2.EmitterType != 3)
                            throw new InvalidDataException($"Unknown ADL ExternalSound type {NewUStruct2.EmitterType}.");
                        NewUStruct.Sound.ExternalSounds.Add(NewUStruct2);
                    }
                    stream.Position = TempPos;
                    HashSounds.Add(NewUStruct);
                }

            }
        }

        public void Save(string path)
        {
            //Generate UStruct1 List
            ExternalSoundsList = new List<SoundData>();
            for (int i = 0; i < HashSounds.Count; i++)
            {
                var TempUStruct = HashSounds[i];
                bool Exists = false;

                for (int a = 0; a < ExternalSoundsList.Count; a++)
                {
                    var first = ExternalSoundsList[a];
                    var second = TempUStruct.Sound;

                    if (first.CollisonSound == second.CollisonSound && first.ExternalSounds.Count == second.ExternalSounds.Count)
                    {
                        if(first.ExternalSounds.Count==0)
                        {
                            Exists = true;
                            TempUStruct.ExternalSoundIndex = a;
                            break;
                        }
                        bool TestFail = false;
                        for (int b = 0; b < first.ExternalSounds.Count; b++)
                        {
                            if (first.ExternalSounds[b].EmitterType != second.ExternalSounds[b].EmitterType
                                || first.ExternalSounds[b].SoundIndex != second.ExternalSounds[b].SoundIndex
                                || first.ExternalSounds[b].Offset != second.ExternalSounds[b].Offset
                                || first.ExternalSounds[b].Radius != second.ExternalSounds[b].Radius
                                || first.ExternalSounds[b].U6 != second.ExternalSounds[b].U6
                                || first.ExternalSounds[b].U7 != second.ExternalSounds[b].U7
                                || first.ExternalSounds[b].U8 != second.ExternalSounds[b].U8
                                || first.ExternalSounds[b].U9 != second.ExternalSounds[b].U9
                                || first.ExternalSounds[b].U10 != second.ExternalSounds[b].U10
                                || first.ExternalSounds[b].U11 != second.ExternalSounds[b].U11)
                            {
                                TestFail = true;
                                break;
                            }
                        }
                        if (!TestFail)
                        {
                            Exists = true;
                            TempUStruct.ExternalSoundIndex = a;
                        }
                        else
                        {
                            Exists = false;
                        }
                        break;
                    }
                }

                if(!Exists)
                {
                    ExternalSoundsList.Add(TempUStruct.Sound);
                    TempUStruct.ExternalSoundIndex = ExternalSoundsList.Count - 1;
                }

                HashSounds[i] = TempUStruct;
            }


            MemoryStream stream = new MemoryStream();

            //Write Header
            StreamUtil.WriteBytes(stream, Magic);
            StreamUtil.WriteFloat32(stream, U0);
            StreamUtil.WriteInt32(stream, HashSounds.Count);

            //Skip UStruct 0 and write UStruct 1 Data Making sure to have offset in u1
            stream.Position += HashSounds.Count * 4 * 2;

            for (int i = 0; i < ExternalSoundsList.Count; i++)
            {
                var TempUstruct1 = ExternalSoundsList[i];
                TempUstruct1.Offset = (int)stream.Position;

                StreamUtil.WriteInt32(stream, TempUstruct1.CollisonSound);
                StreamUtil.WriteInt32(stream, TempUstruct1.ExternalSounds.Count);

                for (int a = 0; a < TempUstruct1.ExternalSounds.Count; a++)
                {
                    var sound = TempUstruct1.ExternalSounds[a];
                    StreamUtil.WriteInt32(stream, sound.EmitterType);
                    StreamUtil.WriteInt32(stream, sound.SoundIndex);
                    StreamUtil.WriteVector3(stream, sound.Offset);
                    StreamUtil.WriteFloat32(stream, sound.Radius);
                    if (sound.EmitterType != 3) StreamUtil.WriteFloat32(stream, sound.U6);
                    if (sound.EmitterType == 1 || sound.EmitterType == 2)
                    {
                        StreamUtil.WriteFloat32(stream, sound.U7);
                        StreamUtil.WriteFloat32(stream, sound.U8);
                        StreamUtil.WriteFloat32(stream, sound.U9);
                        StreamUtil.WriteFloat32(stream, sound.U10);
                        StreamUtil.WriteFloat32(stream, sound.U11);
                    }
                    else if (sound.EmitterType != 0 && sound.EmitterType != 3)
                        throw new InvalidDataException($"Unknown ADL ExternalSound type {sound.EmitterType}.");
                }

                ExternalSoundsList[i] = TempUstruct1;
            }

            StreamUtil.WriteUInt8(stream, 0xff);

            //Go back and write 0
            stream.Position = 4 * 3;
            for (int i = 0; i < HashSounds.Count; i++)
            {
                StreamUtil.WriteInt32(stream, HashSounds[i].Hash);
                StreamUtil.WriteInt32(stream, ExternalSoundsList[HashSounds[i].ExternalSoundIndex].Offset);
            }


            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var file = File.Create(path);
            stream.Position = 0;
            stream.CopyTo(file);
            stream.Dispose();
            file.Close();
        }

        public struct HashSound
        {
            public int Hash;
            public int SoundDataOffset;
            public SoundData Sound;

            public int ExternalSoundIndex;
        }


        public struct SoundData
        {
            public int CollisonSound;
            public int ExternalSoundsCount;
            public List<ExternalSound> ExternalSounds;

            public int Offset;
        }

        public struct ExternalSound
        {
            public int EmitterType; //ADLEmitterType
            public int SoundIndex;
            public Vector3 Offset; //cm from the instance
            public float Radius;
            //U6-U11 depend on EmitterType (falloff curves are ADLFalloffCurve):
            //0 - U6 falloff curve
            //1 - U6/U7 the other two half extents (Radius is the first), U8-U10 axis, U11 falloff curve
            //2 - U6-U8 axis, U9 radial curve, U10 cone, U11 angular curve
            //3 - none
            public float U6;
            public float U7;
            public float U8;
            public float U9;
            public float U10;
            public float U11;
        }
    }
}
