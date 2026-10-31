using SSXLibrary.Internal.Utilities;
using SSXLibrary.Internal.Utilities.StreamExtensions;
using System.Globalization;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.Tricky.PS2
{
    public class SSFHandler
    {
        public int U1 = 1966592;
        public int U2 = 1053952;
        public float U3 = 0.006f;
        public int EffectSlotsCount;
        public int EffectSlotsOffset;
        public int PhysicsCount;
        public int PhysicsOffset; 
        public int CollisonModelCount;
        public int CollisonModelOffset;
        public int EffectsCount;
        public int EffectsOffset;
        public int FunctionCount;
        public int FunctionOffset;
        public int ObjectPropertiesCount;
        public int ObjectPropertiesOffset;
        public int InstanceCount;
        public int InstanceOffset;
        public int SplineCount;
        public int SplineOffset;

        public List<EffectSlot> EffectSlots = new List<EffectSlot>();
        public List<PhysicsHeader> PhysicsHeaders = new List<PhysicsHeader>();
        public List<CollisonModelPointer> CollisonModelPointers = new List<CollisonModelPointer>();
        public List<EffectHeaderStruct> EffectHeaders = new List<EffectHeaderStruct>();
        public List<Function> Functions = new List<Function>();
        public List<ObjectPropertiesStruct> ObjectProperties = new List<ObjectPropertiesStruct>();
        public List<Spline> Splines = new List<Spline>();
        public List<int> InstanceState = new List<int>();

        public void Load(string Path)
        {
            using (Stream stream = File.Open(Path, FileMode.Open))
            {
                U1 = StreamUtil.ReadUInt32(stream);
                U2 = StreamUtil.ReadUInt32(stream);
                U3 = StreamUtil.ReadFloat(stream);
                EffectSlotsCount = StreamUtil.ReadUInt32(stream);
                EffectSlotsOffset = StreamUtil.ReadUInt32(stream);
                PhysicsCount = StreamUtil.ReadUInt32(stream);
                PhysicsOffset = StreamUtil.ReadUInt32(stream);
                CollisonModelCount = StreamUtil.ReadUInt32(stream);
                CollisonModelOffset = StreamUtil.ReadUInt32(stream);
                EffectsCount = StreamUtil.ReadUInt32(stream);
                EffectsOffset = StreamUtil.ReadUInt32(stream);
                FunctionCount = StreamUtil.ReadUInt32(stream);
                FunctionOffset = StreamUtil.ReadUInt32(stream);
                ObjectPropertiesCount = StreamUtil.ReadUInt32(stream);
                ObjectPropertiesOffset = StreamUtil.ReadUInt32(stream);
                InstanceCount = StreamUtil.ReadUInt32(stream);
                InstanceOffset = StreamUtil.ReadUInt32(stream);
                SplineCount = StreamUtil.ReadUInt32(stream);
                SplineOffset = StreamUtil.ReadUInt32(stream);

                EffectSlots = new List<EffectSlot>();
                stream.Position = EffectSlotsOffset;
                for (int i = 0; i < EffectSlotsCount; i++)
                {
                    var TempUstruct1 = new EffectSlot();

                    TempUstruct1.PersistantEffectSlot = StreamUtil.ReadUInt32(stream); //Constant
                    TempUstruct1.CollisionEffectSlot = StreamUtil.ReadUInt32(stream); //Collsion
                    TempUstruct1.Slot3 = StreamUtil.ReadUInt32(stream);
                    TempUstruct1.Slot4 = StreamUtil.ReadUInt32(stream);
                    TempUstruct1.EffectTriggerSlot = StreamUtil.ReadUInt32(stream); //Effect Triggers (Effects and cause these to happen)
                    TempUstruct1.Slot6 = StreamUtil.ReadUInt32(stream);
                    TempUstruct1.Slot7 = StreamUtil.ReadUInt32(stream);
                    EffectSlots.Add(TempUstruct1);
                }
                //Phyisics Objects
                PhysicsHeaders = new List<PhysicsHeader>();
                stream.Position = PhysicsOffset;
                for (int i = 0; i < PhysicsCount; i++)
                {
                    var TempUstruct2 = new PhysicsHeader();

                    TempUstruct2.Offset = StreamUtil.ReadUInt32(stream);
                    TempUstruct2.ByteSize = StreamUtil.ReadUInt32(stream);
                    TempUstruct2.Count = StreamUtil.ReadUInt32(stream); //Probably?

                    var TempPos = stream.Position;
                    stream.Position = TempUstruct2.Offset;
                    TempUstruct2.PhysicsDatas = new List<PhysicsData>();
                    for (int a = 0; a < TempUstruct2.Count; a++)
                    {
                        var NewPhysicsData = new PhysicsData();

                        NewPhysicsData.EndAlignment = StreamUtil.ReadUInt32(stream);
                        NewPhysicsData.OccupancyMaskSize = StreamUtil.ReadUInt32(stream);
                        NewPhysicsData.OccupancyMaskEncoding = StreamUtil.ReadUInt32(stream);
                        NewPhysicsData.OccupancyMaxDepth = StreamUtil.ReadUInt32(stream);

                        NewPhysicsData.Center = StreamUtil.ReadVector3(stream);
                        NewPhysicsData.UFloat3 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.UFloat4 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.UFloat5 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM11 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM12 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM13 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM21 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM22 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM23 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM31 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM32 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InertiaM33 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM11 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM12 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM13 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM21 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM22 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM23 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM31 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM32 = StreamUtil.ReadFloat(stream);
                        NewPhysicsData.InverseInertiaM33 = StreamUtil.ReadFloat(stream);

                        NewPhysicsData.OccupancyLevels = new List<OccupancyLevel>();

                        for (int b = 0; b < NewPhysicsData.OccupancyMaxDepth + 1; b++)
                        {
                            var NewPhysicsStruct = new OccupancyLevel();

                            NewPhysicsStruct.SphereRadius = StreamUtil.ReadFloat(stream);
                            NewPhysicsStruct.ChildOffset = StreamUtil.ReadFloat(stream);
                            NewPhysicsStruct.MaskStride = StreamUtil.ReadUInt32(stream);

                            NewPhysicsData.OccupancyLevels.Add(NewPhysicsStruct);
                        }

                        NewPhysicsData.OccupancyMasks = StreamUtil.ReadBytes(stream, NewPhysicsData.OccupancyMaskSize);
                        stream.Position += NewPhysicsData.EndAlignment;

                        TempUstruct2.PhysicsDatas.Add(NewPhysicsData);
                    }

                    stream.Position = TempPos;

                    PhysicsHeaders.Add(TempUstruct2);
                }

                //Collision Models
                CollisonModelPointers = new List<CollisonModelPointer>();
                stream.Position = CollisonModelOffset;
                int MeshID = 0;
                for (int i = 0; i < CollisonModelCount; i++)
                {
                    var TempUstruct3 = new CollisonModelPointer();

                    TempUstruct3.Offset = StreamUtil.ReadUInt32(stream);
                    TempUstruct3.ByteSize = StreamUtil.ReadUInt32(stream);
                    TempUstruct3.Count = StreamUtil.ReadUInt32(stream); //Probably?

                    var TempPos = stream.Position;
                    stream.Position = TempUstruct3.Offset;

                    //Inset Stuff Here
                    TempUstruct3.Models = new List<CollisonModel>();
                    for (int a = 0; a < TempUstruct3.Count; a++)
                    {
                        var TempModel = new CollisonModel();
                        TempModel.FaceCount = StreamUtil.ReadUInt32(stream);
                        TempModel.VerticeCount = StreamUtil.ReadUInt32(stream);
                        TempModel.VerticeOffsetAlign = StreamUtil.ReadUInt32(stream);

                        TempModel.MeshPath = MeshID.ToString() + ".obj";

                        TempModel.Index = new List<int>();
                        TempModel.Vertices = new List<Vector4>();
                        TempModel.FaceNormals = new List<Vector4>();

                        for (int b = 0; b < TempModel.FaceCount*3; b++)
                        {
                            TempModel.Index.Add(StreamUtil.ReadUInt32(stream));
                        }
                        stream.Position += TempModel.VerticeOffsetAlign;

                        //StreamUtil.AlignBy16(stream);

                        for (int b = 0; b < TempModel.VerticeCount; b++)
                        {
                            TempModel.Vertices.Add(StreamUtil.ReadVector4(stream));
                        }

                        for (int b = 0; b < TempModel.FaceCount; b++)
                        {
                            TempModel.FaceNormals.Add(StreamUtil.ReadVector4(stream));
                        }


                        TempUstruct3.Models.Add(TempModel);
                        MeshID++;
                    }

                    stream.Position = TempPos;

                    CollisonModelPointers.Add(TempUstruct3);
                }

                Functions = new List<Function>();
                stream.Position = FunctionOffset;
                for (int i = 0; i < FunctionCount; i++)
                {
                    var TempFunction = new Function();
                    TempFunction.Count = StreamUtil.ReadUInt32(stream);
                    TempFunction.Offset = StreamUtil.ReadUInt32(stream); //Offset
                    TempFunction.FunctionName = StreamUtil.ReadString(stream, 16);
                    Functions.Add(TempFunction);
                }

                EffectHeaders = new List<EffectHeaderStruct>();
                stream.Position = EffectsOffset;
                for (int i = 0; i < EffectsCount; i++)
                {
                    var TempUstruct4 = new EffectHeaderStruct();
                    TempUstruct4.EffectCount = StreamUtil.ReadUInt32(stream); //Type
                    TempUstruct4.EffectOffset = StreamUtil.ReadUInt32(stream); //Offset
                    EffectHeaders.Add(TempUstruct4);
                }

                //Effects Start Here
                int EffectStartPos = (int)stream.Position;

                for (int i = 0; i < EffectHeaders.Count; i++)
                {
                    stream.Position = EffectHeaders[i].EffectOffset + EffectStartPos;
                    var TempEffect = EffectHeaders[i];
                    TempEffect.Effects = new List<Effect>();

                    for (int a = 0; a < TempEffect.EffectCount; a++)
                    {
                        var NewEffect = LoadEffectsData(stream);

                        if(NewEffect==null)
                        {
                            break;
                        }

                        TempEffect.Effects.Add(NewEffect.Value);
                    }

                    EffectHeaders[i] = TempEffect;
                }

                for (int i = 0; i < Functions.Count; i++)
                {
                    var Function = Functions[i];
                    stream.Position = Function.Offset + EffectStartPos;
                    Function.Effects = new List<Effect>();

                    for (int a = 0; a < Function.Count; a++)
                    {
                        var NewEffect = LoadEffectsData(stream);

                        if (NewEffect == null)
                        {
                            break;
                        }

                        Function.Effects.Add(NewEffect.Value);
                    }

                    Functions[i] = Function;
                }



                //Appears to do nothing
                ObjectProperties = new List<ObjectPropertiesStruct>();
                stream.Position = ObjectPropertiesOffset;
                for (int i = 0; i < ObjectPropertiesCount; i++)
                {
                    var TempUstruct5 = new ObjectPropertiesStruct();
                    TempUstruct5.CollisionResponseMass = StreamUtil.ReadFloat(stream);

                    TempUstruct5.PlayerBounce = StreamUtil.ReadFloat(stream);

                    TempUstruct5.U2 = StreamUtil.ReadUInt16(stream);
                    TempUstruct5.BitFlags = StreamUtil.ReadUInt16(stream);

                    TempUstruct5.SurfaceType = StreamUtil.ReadUInt32(stream); //-1

                    TempUstruct5.CollsionMode = StreamUtil.ReadInt16(stream);

                    if (TempUstruct5.CollsionMode != 3)
                    {
                        TempUstruct5.PhysicsIndex = -1;
                        TempUstruct5.CollisonModelIndex = StreamUtil.ReadInt16(stream);
                        TempUstruct5.EffectSlotIndex = StreamUtil.ReadInt16(stream);
                    }
                    else
                    {
                        TempUstruct5.CollisonModelIndex = -1;
                        TempUstruct5.PhysicsIndex = StreamUtil.ReadInt16(stream);
                        TempUstruct5.EffectSlotIndex = StreamUtil.ReadInt16(stream);
                    }
                    TempUstruct5.U8 = StreamUtil.ReadInt16(stream);
                    ObjectProperties.Add(TempUstruct5);
                }

                //Breaks Collison, Animation, Material Visablity
                InstanceState = new List<int>();
                stream.Position = InstanceOffset;
                for (int i = 0; i < InstanceCount; i++)
                {
                    InstanceState.Add(StreamUtil.ReadUInt32(stream));
                }

                Splines = new List<Spline>();
                stream.Position = SplineOffset;
                for (int i = 0; i < SplineCount; i++)
                {
                    var TempUstruct6 = new Spline();
                    TempUstruct6.U1 = StreamUtil.ReadInt16(stream);
                    TempUstruct6.RailFlags = StreamUtil.ReadInt16(stream);
                    TempUstruct6.SplineStyle = StreamUtil.ReadUInt32(stream); 
                    Splines.Add(TempUstruct6);
                }

                }
        }

        public Effect? LoadEffectsData(Stream stream)
        {
            var NewEffect = new Effect();

            //Debug Info
            NewEffect.Offset = (int)stream.Position;

            NewEffect.MainType = StreamUtil.ReadUInt32(stream);
            NewEffect.ByteSize = StreamUtil.ReadUInt32(stream);

            if (NewEffect.MainType == 0)
            {
                var NewMainType = new Type0();
                NewMainType.SubType = StreamUtil.ReadUInt32(stream);

                if (NewMainType.SubType == 0)
                {
                    var NewSubType = new RollerEffect();
                    NewSubType.Mass = StreamUtil.ReadFloat(stream);
                    NewSubType.U1 = StreamUtil.ReadFloat(stream);
                    NewSubType.U2 = StreamUtil.ReadFloat(stream);
                    NewSubType.LaunchDirection = StreamUtil.ReadVector3(stream);
                    NewMainType.Roller = NewSubType;
                }
                else
                if (NewMainType.SubType == 2)
                {
                    NewMainType.Debounce = StreamUtil.ReadFloat(stream);
                }
                else
                if (NewMainType.SubType == 5)
                {
                    NewMainType.DeadNodeMode = StreamUtil.ReadUInt32(stream);
                }
                else
                if (NewMainType.SubType == 6)
                {
                    var NewSubType = new CounterEffect(); //Counter
                    NewSubType.Count = StreamUtil.ReadUInt32(stream); //Count
                    NewSubType.U1 = StreamUtil.ReadFloat(stream); 
                    NewMainType.Counter = NewSubType;
                }
                else
                if (NewMainType.SubType == 7)
                {
                    var NewSubType = new BoostEffect();
                    NewSubType.Mode = StreamUtil.ReadUInt32(stream);
                    NewSubType.Window = StreamUtil.ReadFloat(stream);
                    NewSubType.ApproachRate = StreamUtil.ReadFloat(stream);
                    NewSubType.BoostAmount = StreamUtil.ReadFloat(stream);
                    NewSubType.BoostDir = StreamUtil.ReadVector3(stream);
                    NewMainType.Boost = NewSubType;
                }
                else
                if (NewMainType.SubType == 10)
                {
                    var NewSubType = new UVScrolling();
                    NewSubType.Mode = StreamUtil.ReadUInt32(stream); // Mode: 0 linear, 1 eased ping-pong, 2 constant ping-pong
                    NewSubType.Speed = StreamUtil.ReadVector2(stream); // Horizontal and vertical UV units per tick
                    NewSubType.ActiveDuration = StreamUtil.ReadFloat(stream); // Active duration (seconds)
                    NewSubType.PauseDuration = StreamUtil.ReadFloat(stream); // Pause duration (seconds)
                    NewSubType.Lifetime = StreamUtil.ReadFloat(stream); // Total lifetime (seconds; 0 = until slot unload)
                    NewMainType.UVScroll = NewSubType;
                }
                else
                if (NewMainType.SubType == 11)
                {
                    var NewSubType = new TextureFlipEffect();
                    NewSubType.StartFrame = StreamUtil.ReadUInt32(stream);
                    NewSubType.Direction = StreamUtil.ReadUInt32(stream); //Direction 0-Forwards, Anything else-backwards
                    NewSubType.Speed = StreamUtil.ReadFloat(stream);
                    NewSubType.Length = StreamUtil.ReadFloat(stream);
                    NewSubType.PauseDwell = StreamUtil.ReadUInt32(stream); //Pauses on frame? Display pattern?
                    NewMainType.TextureFlip = NewSubType;
                }
                else
                if (NewMainType.SubType == 12)
                {
                    var NewSubType = new FenceFlex();
                    NewSubType.U0 = StreamUtil.ReadUInt32(stream); //Flex Mode?
                    NewSubType.FlexAmmount = StreamUtil.ReadFloat(stream);
                    NewMainType.Fence = NewSubType;
                }
                else
                if (NewMainType.SubType == 13)
                {
                    var NewSubType = new FlagEffect();
                    NewSubType.Variant = StreamUtil.ReadUInt32(stream);
                    NewSubType.Amplitude = StreamUtil.ReadFloat(stream);
                    NewSubType.Wavelength = StreamUtil.ReadFloat(stream);
                    NewSubType.U3 = StreamUtil.ReadFloat(stream);
                    NewMainType.Flag = NewSubType;
                }
                else
                if (NewMainType.SubType == 14)
                {
                    var NewSubType = new CrackedEffect();
                    NewSubType.CrackLifetime = StreamUtil.ReadFloat(stream);
                    NewSubType.Strength = StreamUtil.ReadFloat(stream);
                    NewMainType.Cracked = NewSubType;
                }
                else
                if (NewMainType.SubType == 15)
                {
                    var NewSubType = new LapBoostEffect();
                    NewSubType.ApproachRate = StreamUtil.ReadFloat(stream);
                    NewSubType.TargetSpeed = StreamUtil.ReadFloat(stream);
                    NewSubType.Axis = StreamUtil.ReadVector3(stream);
                    NewMainType.LapBoost = NewSubType;
                }
                else
                if (NewMainType.SubType == 17)
                {
                    var NewSubType = new CrowdBox();
                    NewSubType.U0 = StreamUtil.ReadUInt32(stream);
                    //Both Effect How Many Rows Of People
                    NewSubType.Rows = StreamUtil.ReadUInt32(stream); //Row
                    NewSubType.Columns = StreamUtil.ReadUInt32(stream); //Column
                    NewMainType.CrowdEffect = NewSubType;
                }
                else
                if (NewMainType.SubType == 18)
                {
                    var NewSubType = new ZBoostEffect();
                    NewSubType.ApproachRate = StreamUtil.ReadFloat(stream);
                    NewSubType.TargetSpeed = StreamUtil.ReadFloat(stream);
                    NewSubType.Axis = StreamUtil.ReadVector3(stream);
                    NewSubType.TargetAltitude = StreamUtil.ReadFloat(stream);
                    NewSubType.SnapTolerance = StreamUtil.ReadFloat(stream);
                    NewMainType.ZBoost = NewSubType;
                }
                else
                if (NewMainType.SubType == 20)
                {
                    var NewSubType = new MeshAnimEffect();
                    NewSubType.U0 = StreamUtil.ReadUInt32(stream);
                    NewSubType.FrameStep = StreamUtil.ReadFloat(stream);
                    NewSubType.Duration = StreamUtil.ReadFloat(stream);
                    NewSubType.ThrowDirection = StreamUtil.ReadVector3(stream);
                    NewSubType.VelocityScale = StreamUtil.ReadVector3(stream);
                    NewSubType.DirectionScale = StreamUtil.ReadFloat(stream);

                    NewMainType.MeshAnim = NewSubType;
                }
                else
                if (NewMainType.SubType == 23) 
                {
                    //Movie, Contains no actual data
                } //Done
                else
                if (NewMainType.SubType == 24)
                {
                    var NewSubType = new TubeEndBoostEffect();
                    NewSubType.Mode = StreamUtil.ReadUInt32(stream);
                    NewSubType.Window = StreamUtil.ReadFloat(stream);
                    NewSubType.ApproachRate = StreamUtil.ReadFloat(stream);
                    NewSubType.TargetSpeed = StreamUtil.ReadFloat(stream);
                    NewSubType.BaseAxis = StreamUtil.ReadVector3(stream);
                    NewSubType.Stage1Direction = StreamUtil.ReadVector3(stream);
                    NewSubType.Stage2Direction = StreamUtil.ReadVector3(stream);
                    NewSubType.Stage3Direction = StreamUtil.ReadVector3(stream);
                    NewSubType.Stage1Speed = StreamUtil.ReadFloat(stream);
                    NewSubType.Stage2Speed = StreamUtil.ReadFloat(stream);
                    NewSubType.Stage3Speed = StreamUtil.ReadFloat(stream);

                    NewMainType.TubeEndBoost = NewSubType;
                }
                else if (NewMainType.SubType == 256)
                {
                    var NewSubType = new AnimObjectEffect();
                    NewSubType.LoopMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.WindowStart = StreamUtil.ReadFloat(stream);
                    NewSubType.WindowEnd = StreamUtil.ReadFloat(stream);
                    NewSubType.Rate = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomRateUpper = StreamUtil.ReadFloat(stream);
                    NewSubType.Debounce = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomStart = StreamUtil.ReadUInt32(stream);
                    NewSubType.DirectionMode = StreamUtil.ReadUInt32(stream);

                    NewMainType.AnimObject = NewSubType;
                }
                else if (NewMainType.SubType == 257)
                {
                    var NewSubType = new AnimDeltaEffect();
                    NewSubType.LoopMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.WindowStart = StreamUtil.ReadFloat(stream);
                    NewSubType.WindowEnd = StreamUtil.ReadFloat(stream);
                    NewSubType.Rate = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomRateUpper = StreamUtil.ReadFloat(stream);
                    NewSubType.Debounce = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomStart = StreamUtil.ReadUInt32(stream);
                    NewSubType.DirectionMode = StreamUtil.ReadUInt32(stream);

                    NewMainType.AnimDelta = NewSubType;
                }
                else if (NewMainType.SubType == 258)
                {
                    var NewSubType = new AnimComboEffect();
                    NewSubType.LoopMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.WindowStart = StreamUtil.ReadFloat(stream);
                    NewSubType.WindowEnd = StreamUtil.ReadFloat(stream);
                    NewSubType.Rate = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomRateUpper = StreamUtil.ReadFloat(stream);
                    NewSubType.Debounce = StreamUtil.ReadFloat(stream);
                    NewSubType.RandomStart = StreamUtil.ReadUInt32(stream);
                    NewSubType.DirectionMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.ComboWindowStart = StreamUtil.ReadFloat(stream);
                    NewSubType.ComboWindowEnd = StreamUtil.ReadFloat(stream);
                    NewSubType.ComboRate = StreamUtil.ReadFloat(stream);
                    NewSubType.ComboEndMode = StreamUtil.ReadUInt32(stream);

                    NewMainType.AnimCombo = NewSubType;
                }
                else
                {
                    //MessageBox.Show("Missing Type " + NewEffect.MainType.ToString() + "," + NewMainType.SubType.ToString());
                    return null;
                }
                NewEffect.type0 = NewMainType;
            }
            else if (NewEffect.MainType == 2)
            {
                var NewMainType = new Type2();
                NewMainType.SubType = StreamUtil.ReadUInt32(stream);

                if (NewMainType.SubType == 0)
                {
                    NewMainType.Emitter = ReadEmitter(stream);
                }
                else if (NewMainType.SubType == 1)
                {
                    var NewSubType = new SplinePathAnimation();

                    NewSubType.SplineIndex = StreamUtil.ReadUInt32(stream);
                    NewSubType.EndMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.OrientationMode = StreamUtil.ReadUInt32(stream);
                    NewSubType.InstanceCount = StreamUtil.ReadUInt32(stream);

                    NewSubType.AnimationSpeed = StreamUtil.ReadFloat(stream);
                    NewSubType.YawOffset = StreamUtil.ReadFloat(stream);
                    NewSubType.ShowRouteLine = StreamUtil.ReadUInt32(stream);
                    NewSubType.RouteLineColour = ReadColourARGB(stream);

                    NewMainType.SplineAnimation = NewSubType;
                }
                else if (NewMainType.SubType == 2)
                {
                    NewMainType.CollisionEmitter = ReadEmitter(stream);
                }
                else
                {
                    //MessageBox.Show("Missing Type " + NewEffect.MainType.ToString() + "," + NewMainType.SubType.ToString());
                    return null;
                }

                NewEffect.type2 = NewMainType;
            }
            else if (NewEffect.MainType == 3)
            {
                var NewMainType = new Type3();

                NewMainType.Command = StreamUtil.ReadUInt32(stream);
                NewMainType.Parameter = StreamUtil.ReadFloat(stream);

                NewEffect.type3 = NewMainType;
            }
            else if (NewEffect.MainType == 4)
            {
                NewEffect.WaitTime = StreamUtil.ReadFloat(stream);
            } //Done
            else if (NewEffect.MainType == 5)
            {
                var NewMainType = new Type5();

                NewMainType.Mode = StreamUtil.ReadUInt32(stream);
                NewMainType.Compare = StreamUtil.ReadUInt32(stream);
                NewMainType.Threshold = StreamUtil.ReadFloat(stream);

                NewEffect.type5 = NewMainType;
            }
            else if (NewEffect.MainType == 7) //Done
            {
                var NewMainType = new InstanceEffect();

                NewMainType.InstanceIndex = StreamUtil.ReadUInt32(stream);
                NewMainType.EffectIndex = StreamUtil.ReadUInt32(stream);

                NewEffect.Instance = NewMainType;
            } //Done
            else if (NewEffect.MainType == 8) //Done
            {
                NewEffect.SoundPlay = StreamUtil.ReadUInt32(stream);
            }
            else if (NewEffect.MainType == 9)
            {
                var NewMainType = new Type9();

                NewMainType.Command = StreamUtil.ReadUInt32(stream);
                NewMainType.Parameter = StreamUtil.ReadFloat(stream);

                NewEffect.type9 = NewMainType;
            }
            else if (NewEffect.MainType == 13)
            {
                NewEffect.type13 = StreamUtil.ReadFloat(stream);
            }
            else if (NewEffect.MainType == 14)
            {
                NewEffect.MultiplierScore = StreamUtil.ReadFloat(stream);
            } //Done
            else if (NewEffect.MainType == 17)
            {
                NewEffect.SpeedBoostDuration = StreamUtil.ReadFloat(stream);
            }
            else if (NewEffect.MainType == 18)
            {
                NewEffect.TrickBoostDuration = StreamUtil.ReadFloat(stream);
            }
            else if (NewEffect.MainType == 21)
            {
                NewEffect.FunctionRunIndex = StreamUtil.ReadUInt32(stream);
            } //Done
            else if (NewEffect.MainType == 24)
            {
                NewEffect.TeleportInstanceIndex = StreamUtil.ReadUInt32(stream);
            } //Done
            else if (NewEffect.MainType == 25)
            {
                var NewMainType = new SplineEffect();

                NewMainType.SplineIndex = StreamUtil.ReadUInt32(stream);
                NewMainType.Effect = StreamUtil.ReadUInt32(stream);

                NewEffect.Spline = NewMainType;
            } //Done
            else if (NewEffect.MainType == 12)
            {
                var NewMainType = new HUDTextEffect();

                NewMainType.HudRed = StreamUtil.ReadFloat(stream);
                NewMainType.HudGreen = StreamUtil.ReadFloat(stream);
                NewMainType.HudBlue = StreamUtil.ReadFloat(stream);
                NewMainType.HudText = stream.ReadUtf16NullTerminated();

                NewEffect.hudTextEffect = NewMainType;
            }
            else
            {
                //An opcode with no branch here. The engine dispatches more main types than the
                //shipped corpus authors, so this is a real case rather than a corrupt-file one:
                //keep the bytes and let the writer put them back. Returning null instead - which is
                //what this did - makes the caller break out of the chain loop, so the unknown node
                //AND every node after it in that chain vanish with no diagnostic.
                NewEffect.UnknownPayload = StreamUtil.ReadBytes(stream, PayloadLength(NewEffect));
            }

            return NewEffect;
        }

        //Main type 2 sub types 0 and 2 share this layout
        static EmitterEffect ReadEmitter(Stream stream)
        {
            var NewEmitter = new EmitterEffect();

            NewEmitter.ParticleCount = StreamUtil.ReadUInt32(stream);
            NewEmitter.TrailCopies = StreamUtil.ReadUInt32(stream);
            NewEmitter.EmissionWindow = StreamUtil.ReadFloat(stream);
            NewEmitter.TimeScale = StreamUtil.ReadFloat(stream);

            NewEmitter.SizeCenter = StreamUtil.ReadFloat(stream);
            NewEmitter.ParticleLifeCenter = StreamUtil.ReadFloat(stream);
            NewEmitter.SizeSpan = StreamUtil.ReadFloat(stream);
            NewEmitter.ParticleLifeSpan = StreamUtil.ReadFloat(stream);

            NewEmitter.TrailSpacing = StreamUtil.ReadFloat(stream);
            NewEmitter.Origin = StreamUtil.ReadVector3(stream);
            NewEmitter.SpawnAxisA = StreamUtil.ReadVector3(stream);
            NewEmitter.SpawnAxisB = StreamUtil.ReadVector3(stream);
            NewEmitter.VelocityBase = StreamUtil.ReadVector3(stream);
            NewEmitter.VelocityAxisA = StreamUtil.ReadVector3(stream);
            NewEmitter.VelocityAxisB = StreamUtil.ReadVector3(stream);
            NewEmitter.VelocityAxisC = StreamUtil.ReadVector3(stream);
            NewEmitter.Gravity = StreamUtil.ReadVector3(stream);

            NewEmitter.ColourStop1 = ReadColourARGB(stream);
            NewEmitter.ColourStop2 = ReadColourARGB(stream);
            NewEmitter.ColourStop3 = ReadColourARGB(stream);
            NewEmitter.ColourStop4 = ReadColourARGB(stream);

            NewEmitter.SpriteIndex = StreamUtil.ReadUInt32(stream);
            NewEmitter.BlendMode = StreamUtil.ReadUInt32(stream);

            return NewEmitter;
        }

        static void WriteEmitter(Stream stream, EmitterEffect Emitter)
        {
            StreamUtil.WriteInt32(stream, Emitter.ParticleCount);
            StreamUtil.WriteInt32(stream, Emitter.TrailCopies);
            StreamUtil.WriteFloat32(stream, Emitter.EmissionWindow);
            StreamUtil.WriteFloat32(stream, Emitter.TimeScale);

            StreamUtil.WriteFloat32(stream, Emitter.SizeCenter);
            StreamUtil.WriteFloat32(stream, Emitter.ParticleLifeCenter);
            StreamUtil.WriteFloat32(stream, Emitter.SizeSpan);
            StreamUtil.WriteFloat32(stream, Emitter.ParticleLifeSpan);

            StreamUtil.WriteFloat32(stream, Emitter.TrailSpacing);
            StreamUtil.WriteVector3(stream, Emitter.Origin);
            StreamUtil.WriteVector3(stream, Emitter.SpawnAxisA);
            StreamUtil.WriteVector3(stream, Emitter.SpawnAxisB);
            StreamUtil.WriteVector3(stream, Emitter.VelocityBase);
            StreamUtil.WriteVector3(stream, Emitter.VelocityAxisA);
            StreamUtil.WriteVector3(stream, Emitter.VelocityAxisB);
            StreamUtil.WriteVector3(stream, Emitter.VelocityAxisC);
            StreamUtil.WriteVector3(stream, Emitter.Gravity);

            WriteColourARGB(stream, Emitter.ColourStop1);
            WriteColourARGB(stream, Emitter.ColourStop2);
            WriteColourARGB(stream, Emitter.ColourStop3);
            WriteColourARGB(stream, Emitter.ColourStop4);

            StreamUtil.WriteInt32(stream, Emitter.SpriteIndex);
            StreamUtil.WriteInt32(stream, Emitter.BlendMode);
        }

        //Effect colours are stored A,R,G,B. In memory the Vector4 is R,G,B,A
        static Vector4 ReadColourARGB(Stream stream)
        {
            float Alpha = StreamUtil.ReadFloat(stream);
            return new Vector4(StreamUtil.ReadVector3(stream), Alpha);
        }

        static void WriteColourARGB(Stream stream, Vector4 Colour)
        {
            StreamUtil.WriteFloat32(stream, Colour.W);
            StreamUtil.WriteFloat32(stream, Colour.X);
            StreamUtil.WriteFloat32(stream, Colour.Y);
            StreamUtil.WriteFloat32(stream, Colour.Z);
        }

        /// <summary>Payload bytes of one effect node: its size field counts the whole node, main type
        /// and size included, so the payload is what is left after those two words.</summary>
        /// <remarks>A size that cannot be a whole node would seek the chain loop backwards and hang
        /// it, so this refuses rather than trusting the file.</remarks>
        /// <summary>R, G and B as f32, the fixed head of a main-type-12 payload.</summary>
        const int HudColourBytes = 12;

        static int PayloadLength(Effect effect)
        {
            const int HeaderBytes = 8; // MainType + ByteSize

            if (effect.ByteSize < HeaderBytes || (effect.ByteSize % 4) != 0)
            {
                throw new InvalidDataException(
                    $"SSF effect at 0x{effect.Offset:X} declares ByteSize {effect.ByteSize}; a node is at " +
                    $"least {HeaderBytes} bytes and always a multiple of 4 (the engine reads the next " +
                    "node's main type with a word load, which faults when unaligned).");
            }

            return effect.ByteSize - HeaderBytes;
        }

        public void SaveEffectData(Stream stream, Effect EffectData)
        {
            StreamUtil.WriteInt32(stream, EffectData.MainType);
            long ByteSize = stream.Position;
            stream.Position += 4;

            if(EffectData.MainType == 0)
            {
                var Type0Temp = EffectData.type0.Value;
                StreamUtil.WriteInt32(stream, Type0Temp.SubType);
                if (EffectData.type0.Value.SubType == 0)
                {
                    StreamUtil.WriteFloat32(stream, Type0Temp.Roller.Value.Mass);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Roller.Value.U1);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Roller.Value.U2);
                    StreamUtil.WriteVector3(stream, Type0Temp.Roller.Value.LaunchDirection);
                }
                else if (EffectData.type0.Value.SubType == 2)
                {
                    StreamUtil.WriteFloat32(stream, Type0Temp.Debounce);
                }
                else if (EffectData.type0.Value.SubType == 5)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.DeadNodeMode);
                }
                else if (EffectData.type0.Value.SubType == 6)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.Counter.Value.Count);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Counter.Value.U1);
                }
                else if (EffectData.type0.Value.SubType == 7)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.Boost.Value.Mode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Boost.Value.Window);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Boost.Value.ApproachRate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Boost.Value.BoostAmount);
                    StreamUtil.WriteVector3(stream, Type0Temp.Boost.Value.BoostDir);
                }
                else if (EffectData.type0.Value.SubType == 10)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.UVScroll.Value.Mode);
                    StreamUtil.WriteVector2(stream, Type0Temp.UVScroll.Value.Speed);
                    StreamUtil.WriteFloat32(stream, Type0Temp.UVScroll.Value.ActiveDuration);
                    StreamUtil.WriteFloat32(stream, Type0Temp.UVScroll.Value.PauseDuration);
                    StreamUtil.WriteFloat32(stream, Type0Temp.UVScroll.Value.Lifetime);
                }
                else if (EffectData.type0.Value.SubType == 11)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.TextureFlip.Value.StartFrame);
                    StreamUtil.WriteInt32(stream, Type0Temp.TextureFlip.Value.Direction);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TextureFlip.Value.Speed);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TextureFlip.Value.Length);
                    StreamUtil.WriteInt32(stream, Type0Temp.TextureFlip.Value.PauseDwell);
                }
                else if (EffectData.type0.Value.SubType == 12)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.Fence.Value.U0);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Fence.Value.FlexAmmount);
                }
                else if (EffectData.type0.Value.SubType == 13)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.Flag.Value.Variant);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Flag.Value.Amplitude);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Flag.Value.Wavelength);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Flag.Value.U3);
                }
                else if (EffectData.type0.Value.SubType == 14)
                {
                    StreamUtil.WriteFloat32(stream, Type0Temp.Cracked.Value.CrackLifetime);
                    StreamUtil.WriteFloat32(stream, Type0Temp.Cracked.Value.Strength);
                }
                else if (EffectData.type0.Value.SubType == 15)
                {
                    StreamUtil.WriteFloat32(stream, Type0Temp.LapBoost.Value.ApproachRate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.LapBoost.Value.TargetSpeed);
                    StreamUtil.WriteVector3(stream, Type0Temp.LapBoost.Value.Axis);
                }
                else if (EffectData.type0.Value.SubType == 17)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.CrowdEffect.Value.U0);
                    StreamUtil.WriteInt32(stream, Type0Temp.CrowdEffect.Value.Rows);
                    StreamUtil.WriteInt32(stream, Type0Temp.CrowdEffect.Value.Columns);
                }
                else if (EffectData.type0.Value.SubType == 18)
                {
                    StreamUtil.WriteFloat32(stream, Type0Temp.ZBoost.Value.ApproachRate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.ZBoost.Value.TargetSpeed);
                    StreamUtil.WriteVector3(stream, Type0Temp.ZBoost.Value.Axis);
                    StreamUtil.WriteFloat32(stream, Type0Temp.ZBoost.Value.TargetAltitude);
                    StreamUtil.WriteFloat32(stream, Type0Temp.ZBoost.Value.SnapTolerance);
                }
                else if (EffectData.type0.Value.SubType == 20)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.MeshAnim.Value.U0);
                    StreamUtil.WriteFloat32(stream, Type0Temp.MeshAnim.Value.FrameStep);
                    StreamUtil.WriteFloat32(stream, Type0Temp.MeshAnim.Value.Duration);
                    StreamUtil.WriteVector3(stream, Type0Temp.MeshAnim.Value.ThrowDirection);
                    StreamUtil.WriteVector3(stream, Type0Temp.MeshAnim.Value.VelocityScale);
                    StreamUtil.WriteFloat32(stream, Type0Temp.MeshAnim.Value.DirectionScale);
                }
                else if (EffectData.type0.Value.SubType == 23)
                {
                    //DONE
                }
                else if (EffectData.type0.Value.SubType == 24)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.TubeEndBoost.Value.Mode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.Window);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.ApproachRate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.TargetSpeed);
                    StreamUtil.WriteVector3(stream, Type0Temp.TubeEndBoost.Value.BaseAxis);
                    StreamUtil.WriteVector3(stream, Type0Temp.TubeEndBoost.Value.Stage1Direction);
                    StreamUtil.WriteVector3(stream, Type0Temp.TubeEndBoost.Value.Stage2Direction);
                    StreamUtil.WriteVector3(stream, Type0Temp.TubeEndBoost.Value.Stage3Direction);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.Stage1Speed);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.Stage2Speed);
                    StreamUtil.WriteFloat32(stream, Type0Temp.TubeEndBoost.Value.Stage3Speed);
                }
                else if (EffectData.type0.Value.SubType == 256)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimObject.Value.LoopMode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimObject.Value.WindowStart);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimObject.Value.WindowEnd);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimObject.Value.Rate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimObject.Value.RandomRateUpper);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimObject.Value.Debounce);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimObject.Value.RandomStart);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimObject.Value.DirectionMode);
                }
                else if (EffectData.type0.Value.SubType == 257)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimDelta.Value.LoopMode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimDelta.Value.WindowStart);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimDelta.Value.WindowEnd);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimDelta.Value.Rate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimDelta.Value.RandomRateUpper);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimDelta.Value.Debounce);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimDelta.Value.RandomStart);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimDelta.Value.DirectionMode);
                }
                else if (EffectData.type0.Value.SubType == 258)
                {
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimCombo.Value.LoopMode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.WindowStart);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.WindowEnd);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.Rate);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.RandomRateUpper);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.Debounce);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimCombo.Value.RandomStart);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimCombo.Value.DirectionMode);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.ComboWindowStart);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.ComboWindowEnd);
                    StreamUtil.WriteFloat32(stream, Type0Temp.AnimCombo.Value.ComboRate);
                    StreamUtil.WriteInt32(stream, Type0Temp.AnimCombo.Value.ComboEndMode);
                }
                else
                {
                    //MessageBox.Show("ERROR MISSING DATA CANT SAVE SSF EFFECT " + EffectData.MainType + ", " + EffectData.type0.Value.SubType);
                }
            }
            else if (EffectData.MainType == 2)
            {
                StreamUtil.WriteInt32(stream, EffectData.type2.Value.SubType);

                if(EffectData.type2.Value.SubType == 0)
                {
                    WriteEmitter(stream, EffectData.type2.Value.Emitter.Value);
                }
                else if (EffectData.type2.Value.SubType == 1)
                {
                    StreamUtil.WriteInt32(stream, EffectData.type2.Value.SplineAnimation.Value.SplineIndex);
                    StreamUtil.WriteInt32(stream, EffectData.type2.Value.SplineAnimation.Value.EndMode);
                    StreamUtil.WriteInt32(stream, EffectData.type2.Value.SplineAnimation.Value.OrientationMode);
                    StreamUtil.WriteInt32(stream, EffectData.type2.Value.SplineAnimation.Value.InstanceCount);

                    StreamUtil.WriteFloat32(stream, EffectData.type2.Value.SplineAnimation.Value.AnimationSpeed);
                    StreamUtil.WriteFloat32(stream, EffectData.type2.Value.SplineAnimation.Value.YawOffset);
                    StreamUtil.WriteInt32(stream, EffectData.type2.Value.SplineAnimation.Value.ShowRouteLine);
                    WriteColourARGB(stream, EffectData.type2.Value.SplineAnimation.Value.RouteLineColour);
                }
                else if (EffectData.type2.Value.SubType == 2)
                {
                    WriteEmitter(stream, EffectData.type2.Value.CollisionEmitter.Value);
                }
                else
                {
                    //MessageBox.Show("ERROR MISSING DATA CANT SAVE SSF EFFECT " + EffectData.MainType + ", " + EffectData.type2.Value.SubType);
                }
            }
            else if (EffectData.MainType == 3)
            {
                StreamUtil.WriteInt32(stream, EffectData.type3.Value.Command);
                StreamUtil.WriteFloat32(stream, EffectData.type3.Value.Parameter);
            }
            else if (EffectData.MainType == 4)
            {
                StreamUtil.WriteFloat32(stream, EffectData.WaitTime);
            }
            else if (EffectData.MainType == 5)
            {
                StreamUtil.WriteInt32(stream, EffectData.type5.Value.Mode);
                StreamUtil.WriteInt32(stream, EffectData.type5.Value.Compare);
                StreamUtil.WriteFloat32(stream, EffectData.type5.Value.Threshold);
            }
            else if (EffectData.MainType == 7)
            {
                StreamUtil.WriteInt32(stream, EffectData.Instance.Value.InstanceIndex);
                StreamUtil.WriteInt32(stream, EffectData.Instance.Value.EffectIndex);
            }
            else if (EffectData.MainType == 8)
            {
                StreamUtil.WriteInt32(stream, EffectData.SoundPlay);
            }
            else if (EffectData.MainType == 9)
            {
                StreamUtil.WriteInt32(stream, EffectData.type9.Value.Command);
                StreamUtil.WriteFloat32(stream, EffectData.type9.Value.Parameter);
            }
            else if (EffectData.MainType == 13)
            {
                StreamUtil.WriteFloat32(stream, EffectData.type13);
            }
            else if (EffectData.MainType == 14)
            {
                StreamUtil.WriteFloat32(stream, EffectData.MultiplierScore);
            }
            else if (EffectData.MainType == 17)
            {
                StreamUtil.WriteFloat32(stream, EffectData.SpeedBoostDuration);
            }
            else if (EffectData.MainType == 18)
            {
                StreamUtil.WriteFloat32(stream, EffectData.TrickBoostDuration);
            }
            else if (EffectData.MainType == 21)
            {
                StreamUtil.WriteInt32(stream, EffectData.FunctionRunIndex);
            }
            else if (EffectData.MainType == 24)
            {
                StreamUtil.WriteInt32(stream, EffectData.TeleportInstanceIndex);
            }
            else if (EffectData.MainType == 25)
            {
                StreamUtil.WriteInt32(stream, EffectData.Spline.Value.SplineIndex);
                StreamUtil.WriteInt32(stream, EffectData.Spline.Value.Effect);
            }
            else if (EffectData.MainType == 12)
            {
                //Colour first, then UTF-16LE text, NUL-terminated, then zero-padded so the node stays a
                //multiple of 4 bytes. The padding is load bearing rather than tidy: the chain walker
                //advances by the size field and reads the next node's main type with a word load, which
                //faults on the EE if that lands unaligned.
                StreamUtil.WriteFloat32(stream, EffectData.hudTextEffect.Value.HudRed);
                StreamUtil.WriteFloat32(stream, EffectData.hudTextEffect.Value.HudGreen);
                StreamUtil.WriteFloat32(stream, EffectData.hudTextEffect.Value.HudBlue);
                StreamUtil.WriteString16(stream, EffectData.hudTextEffect.Value.HudText ?? "");
                StreamUtil.WriteInt16(stream, 0);

                long PayloadBytes = stream.Position - (ByteSize + 4);
                int Padding = (int)((4 - (PayloadBytes % 4)) % 4);
                if (Padding != 0)
                {
                    StreamUtil.WriteBytes(stream, new byte[Padding]);
                }
            }
            else if (EffectData.UnknownPayload != null)
            {
                //An opcode the reader had no branch for. Put its bytes back exactly, so a main type
                //this library does not understand survives a round trip instead of being erased.
                StreamUtil.WriteBytes(stream, EffectData.UnknownPayload);
            }
            else
            {
                //MessageBox.Show("ERROR MISSING DATA CANT SAVE SSF EFFECT " + EffectData.MainType);
            }


            long TempPos = stream.Position;
            stream.Position = ByteSize;
            StreamUtil.WriteInt32(stream, (int)(TempPos - ByteSize)+4);
            stream.Position = TempPos;
        }

        public void Save(string path)
        {
            MemoryStream stream = new MemoryStream();

            stream.Position = 76;

            EffectSlotsOffset = (int)stream.Position;
            for (int i = 0; i < EffectSlots.Count; i++)
            {
                StreamUtil.WriteInt32(stream, EffectSlots[i].PersistantEffectSlot);
                StreamUtil.WriteInt32(stream, EffectSlots[i].CollisionEffectSlot);
                StreamUtil.WriteInt32(stream, EffectSlots[i].Slot3);
                StreamUtil.WriteInt32(stream, EffectSlots[i].Slot4);
                StreamUtil.WriteInt32(stream, EffectSlots[i].EffectTriggerSlot);
                StreamUtil.WriteInt32(stream, EffectSlots[i].Slot6);
                StreamUtil.WriteInt32(stream, EffectSlots[i].Slot7);
            }


            PhysicsOffset = (int)stream.Position;
            //Skip passed it all and write shit
            stream.Position += (PhysicsHeaders.Count * 4 * 3) + (CollisonModelPointers.Count * 4 * 3) + (Functions.Count*16 + Functions.Count*2*4) + (EffectHeaders.Count*4*2);

            //Write Effects
            long EffectData = stream.Position;
            for (int i = 0; i < EffectHeaders.Count; i++)
            {
                var TempData = EffectHeaders[i];

                TempData.EffectOffset = (int)(stream.Position - EffectData);

                EffectHeaders[i] = TempData;
                for (int a = 0; a < EffectHeaders[i].Effects.Count; a++)
                {
                    SaveEffectData(stream, EffectHeaders[i].Effects[a]);
                }
            }

            //Write Function
            for (int i = 0; i < Functions.Count; i++)
            {
                var TempData = Functions[i];

                TempData.Offset = (int)(stream.Position - EffectData);
                Functions[i] = TempData;

                for (int a = 0; a < Functions[i].Effects.Count; a++)
                {
                    SaveEffectData(stream, Functions[i].Effects[a]);
                }
            }

            StreamUtil.AlignBy16(stream);
            //stream.Position = PhysicsHeaders[0].Offset;
            //Write Physics
            for (int i = 0; i < PhysicsHeaders.Count; i++)
            {
                var TempHeader = PhysicsHeaders[i];
                TempHeader.Offset = (int)stream.Position;

                for (int a = 0; a < TempHeader.PhysicsDatas.Count; a++)
                {
                    long U0Pos = stream.Position;
                    stream.Position += 4;
                    StreamUtil.WriteInt32(stream, TempHeader.PhysicsDatas[a].OccupancyMasks.Length);
                    StreamUtil.WriteInt32(stream, TempHeader.PhysicsDatas[a].OccupancyMaskEncoding);
                    StreamUtil.WriteInt32(stream, TempHeader.PhysicsDatas[a].OccupancyLevels.Count - 1);

                    StreamUtil.WriteVector3(stream, TempHeader.PhysicsDatas[a].Center);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].UFloat3);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].UFloat4);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].UFloat5);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM11);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM12);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM13);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM21);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM22);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM23);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM31);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM32);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InertiaM33);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM11);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM12);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM13);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM21);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM22);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM23);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM31);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM32);
                    StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].InverseInertiaM33);

                    for (int b = 0; b < TempHeader.PhysicsDatas[a].OccupancyLevels.Count; b++)
                    {
                        StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].OccupancyLevels[b].SphereRadius);
                        StreamUtil.WriteFloat32(stream, TempHeader.PhysicsDatas[a].OccupancyLevels[b].ChildOffset);
                        StreamUtil.WriteInt32(stream, TempHeader.PhysicsDatas[a].OccupancyLevels[b].MaskStride);
                    }

                    StreamUtil.WriteBytes(stream, TempHeader.PhysicsDatas[a].OccupancyMasks);

                    long TempPos = stream.Position;
                    StreamUtil.AlignBy(stream, 4);
                    long TempLen = stream.Position - TempPos;
                    if(TempLen==0)
                    {
                        TempLen = 4;
                        stream.Position += 4;
                    }
                    TempPos = stream.Position;

                    stream.Position = U0Pos;
                    StreamUtil.WriteInt32(stream, (int)TempLen);

                    stream.Position = TempPos;

                }

                TempHeader.ByteSize = (int)(stream.Position - TempHeader.Offset);
                PhysicsHeaders[i] = TempHeader;
            }

            StreamUtil.AlignBy16(stream);
            //Write Collision
            for (int i = 0; i < CollisonModelPointers.Count; i++)
            {
                var TempCollision = CollisonModelPointers[i];
                TempCollision.Offset = (int)stream.Position;
                for (int a = 0; a < TempCollision.Models.Count; a++)
                {
                    var TempModel = TempCollision.Models[a];

                    StreamUtil.WriteInt32(stream, TempModel.Index.Count / 3);
                    StreamUtil.WriteInt32(stream, TempModel.Vertices.Count);
                    long TempPosition = stream.Position;
                    stream.Position += 4;

                    for (int b = 0; b < TempModel.Index.Count; b++)
                    {
                        StreamUtil.WriteInt32(stream, TempModel.Index[b]);
                    }

                    long AlignSize = stream.Position;
                    StreamUtil.AlignBy16(stream);
                    TempModel.VerticeOffsetAlign = (int)(stream.Position - AlignSize);

                    long VerticesOffset = stream.Position;
                    stream.Position = TempPosition;
                    StreamUtil.WriteInt32(stream, TempModel.VerticeOffsetAlign);
                    stream.Position = VerticesOffset;

                    for (int b = 0; b < TempModel.Vertices.Count; b++)
                    {
                        StreamUtil.WriteVector4(stream, TempModel.Vertices[b]);
                    }
                    for (int b = 0; b < TempModel.FaceNormals.Count; b++)
                    {
                        StreamUtil.WriteVector4(stream, TempModel.FaceNormals[b]);
                    }
                }
                TempCollision.ByteSize = (int)(stream.Position - TempCollision.Offset);
                CollisonModelPointers[i] = TempCollision;
            }


            ObjectPropertiesOffset = (int)stream.Position;
            stream.Position = PhysicsOffset;
            for (int i = 0; i < PhysicsHeaders.Count; i++)
            {
                StreamUtil.WriteInt32(stream, PhysicsHeaders[i].Offset);
                StreamUtil.WriteInt32(stream, PhysicsHeaders[i].ByteSize);
                StreamUtil.WriteInt32(stream, PhysicsHeaders[i].PhysicsDatas.Count);
            }

            CollisonModelOffset = (int)stream.Position;
            for (int i = 0; i < CollisonModelPointers.Count; i++)
            {
                StreamUtil.WriteInt32(stream, CollisonModelPointers[i].Offset);
                StreamUtil.WriteInt32(stream, CollisonModelPointers[i].ByteSize);
                StreamUtil.WriteInt32(stream, CollisonModelPointers[i].Models.Count);
            }

            FunctionOffset = (int)stream.Position;
            for (int i = 0; i < Functions.Count; i++)
            {
                StreamUtil.WriteInt32(stream, Functions[i].Effects.Count);
                StreamUtil.WriteInt32(stream, Functions[i].Offset);
                StreamUtil.WriteString(stream, Functions[i].FunctionName, 16);
            }

            EffectsOffset = (int)stream.Position;
            for (int i = 0; i < EffectHeaders.Count; i++)
            {
                StreamUtil.WriteInt32(stream, EffectHeaders[i].Effects.Count);
                StreamUtil.WriteInt32(stream, EffectHeaders[i].EffectOffset);
            }

            //Go Back
            //Write Object Properties
            stream.Position = ObjectPropertiesOffset;
            for (int i = 0; i < ObjectProperties.Count; i++)
            {
                var TempUstruct5 = ObjectProperties[i];

                StreamUtil.WriteFloat32(stream, TempUstruct5.CollisionResponseMass);

                StreamUtil.WriteFloat32(stream, TempUstruct5.PlayerBounce);

                StreamUtil.WriteInt16(stream, TempUstruct5.U2);
                StreamUtil.WriteInt16(stream, TempUstruct5.BitFlags);

                StreamUtil.WriteInt32(stream, TempUstruct5.SurfaceType);

                StreamUtil.WriteInt16(stream, TempUstruct5.CollsionMode);

                if (TempUstruct5.CollsionMode != 3)
                {
                    StreamUtil.WriteInt16(stream, TempUstruct5.CollisonModelIndex);
                }
                else
                {
                    StreamUtil.WriteInt16(stream, TempUstruct5.PhysicsIndex);
                }
                StreamUtil.WriteInt16(stream, TempUstruct5.EffectSlotIndex);
                StreamUtil.WriteInt16(stream, TempUstruct5.U8);
            }

            InstanceOffset = (int)stream.Position;
            for (int i = 0; i < InstanceState.Count; i++)
            {
                StreamUtil.WriteInt32(stream, InstanceState[i]);
            }

            SplineOffset = (int)stream.Position;
            for (int i = 0; i < Splines.Count; i++)
            {
                StreamUtil.WriteInt16(stream, Splines[i].U1);
                StreamUtil.WriteInt16(stream, Splines[i].RailFlags);
                StreamUtil.WriteInt32(stream, Splines[i].SplineStyle);
            }


            stream.Position = 0;

            StreamUtil.WriteInt32(stream, U1);
            StreamUtil.WriteInt32(stream, U2);
            StreamUtil.WriteFloat32(stream, U3);
            StreamUtil.WriteInt32(stream, EffectSlots.Count);
            StreamUtil.WriteInt32(stream, EffectSlotsOffset);
            StreamUtil.WriteInt32(stream, PhysicsHeaders.Count);
            StreamUtil.WriteInt32(stream, PhysicsOffset);
            StreamUtil.WriteInt32(stream, CollisonModelPointers.Count);
            StreamUtil.WriteInt32(stream, CollisonModelOffset);
            StreamUtil.WriteInt32(stream, EffectHeaders.Count);
            StreamUtil.WriteInt32(stream, EffectsOffset);
            StreamUtil.WriteInt32(stream, Functions.Count);
            StreamUtil.WriteInt32(stream, FunctionOffset);
            StreamUtil.WriteInt32(stream, ObjectProperties.Count);
            StreamUtil.WriteInt32(stream, ObjectPropertiesOffset);
            StreamUtil.WriteInt32(stream, InstanceState.Count);
            StreamUtil.WriteInt32(stream, InstanceOffset);
            StreamUtil.WriteInt32(stream, Splines.Count);
            StreamUtil.WriteInt32(stream, SplineOffset);

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

        public void SaveModels(string Path)
        {
            int c = 0;
            for (int a = 0; a < CollisonModelPointers.Count; a++)
            {
                var TempCollision = CollisonModelPointers[a];

                for (int ax = 0; ax < TempCollision.Models.Count; ax++)
                {
                    var Data = TempCollision.Models[ax];

                    string outputString = "";
                    string output = "# Exported From SSX Using SSX Multitool Modder by GlitcherOG \n";

                    outputString += "o Mesh" + c + "\n";
                    for (int b = 0; b < Data.FaceCount; b++)
                    {
                        outputString += "f " + (Data.Index[3*b]+1) + "//" + (b+1).ToString() + " " + (Data.Index[3*b+1]+1) + "//" + (b+1).ToString() + " " + (Data.Index[3*b+2]+1) + "//" + (b+1).ToString() + "\n";
                    }

                    for (int z = 0; z < Data.Vertices.Count; z++)
                    {
                        output += "v " + Data.Vertices[z].X.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Data.Vertices[z].Y.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Data.Vertices[z].Z.ToString(CultureInfo.InvariantCulture.NumberFormat) + "\n";
                    }
                    for (int z = 0; z < Data.FaceNormals.Count; z++)
                    {
                        output += "vn " + Data.FaceNormals[z].X.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Data.FaceNormals[z].Y.ToString(CultureInfo.InvariantCulture.NumberFormat) + " " + Data.FaceNormals[z].Z.ToString(CultureInfo.InvariantCulture.NumberFormat) + "\n";
                    }
                    output += outputString;
                    File.WriteAllText(Path + "/" + Data.MeshPath, output);
                    c++;
                }

            }

        }

        public void LoadModels(string Path)
        {
            for (int i = 0; i < CollisonModelPointers.Count; i++)
            {
                Console.WriteLine("Collision " + (i+1) + "/" + CollisonModelPointers.Count);
                var TempPath = CollisonModelPointers[i];

                for (int a = 0; a < TempPath.Models.Count; a++)
                {
                    var Model = TempPath.Models[a];

                    TempPath.Models[a] = objSSFHandler.LoadModel(Model, Path);
                }

                CollisonModelPointers[i] = TempPath;
            }
        }


        public struct EffectSlot
        {
            public int PersistantEffectSlot;
            public int CollisionEffectSlot;
            public int Slot3;
            public int Slot4;
            public int EffectTriggerSlot;
            public int Slot6;
            public int Slot7;
        }

        public struct PhysicsHeader
        {
            public int Offset;
            public int ByteSize;
            public int Count;

            public List<PhysicsData> PhysicsDatas;
        }

        public struct PhysicsData
        {
            //A mode-3 collision body: an octree of spheres around Center, plus its rigid body mass properties
            public int EndAlignment; //End Bytes
            public int OccupancyMaskSize; //Byte length of OccupancyMasks
            public int OccupancyMaskEncoding; //SSFOccupancyMaskEncoding
            public int OccupancyMaxDepth; //OccupancyLevels count - 1

            public Vector3 Center; //Body space, cm
            public float UFloat3; //UFloat3-5 another point (OpenSlope disagrees on whether it's the centre of mass)

            public float UFloat4;
            public float UFloat5;
            public float InertiaM11; //Inertia and InverseInertia are 3x3 row-major
            public float InertiaM12;

            public float InertiaM13;
            public float InertiaM21;
            public float InertiaM22;
            public float InertiaM23;

            public float InertiaM31;
            public float InertiaM32;
            public float InertiaM33;
            public float InverseInertiaM11;

            public float InverseInertiaM12;
            public float InverseInertiaM13;
            public float InverseInertiaM21;
            public float InverseInertiaM22;

            public float InverseInertiaM23;
            public float InverseInertiaM31;
            public float InverseInertiaM32;
            public float InverseInertiaM33;

            public List<OccupancyLevel> OccupancyLevels; //One per depth

            public byte[] OccupancyMasks; //One child mask byte per node, levels laid out root first
        }

        public struct OccupancyLevel
        {
            public float SphereRadius; //Radius of a node sphere at this depth, a leaf when its mask is 0
            public float ChildOffset; //Offset from the parent centre to a child octant at this depth
            public int MaskStride; //Child mask offset = parent offset + (bit + 1) * MaskStride
        }

        public struct CollisonModelPointer
        {
            public int Offset;
            public int ByteSize;
            public int Count; //Maybe?

            public List<CollisonModel> Models;
        }

        public struct CollisonModel
        {
            public string MeshPath;

            public int FaceCount;
            public int VerticeCount;
            public int VerticeOffsetAlign;

            public List<int> Index;
            public List<Vector4> Vertices;
            public List<Vector4> FaceNormals; //Face Count
        }

        public struct Function
        {
            public int Count;
            public int Offset;
            public string FunctionName;

            public List<Effect> Effects;
        }

        public struct ObjectPropertiesStruct
        {
            public float CollisionResponseMass; //Exactly 0 is pass-through
            public float PlayerBounce;

            public int U2;
            public int BitFlags; //SSFObjectPropertyFlags

            //BitFlags1 - 8
            //1 - Visable
            //2 - 
            //4 -
            //8 -
            //16 -  
            //32 - Player Collison Detect
            //64 - 
            //128 - Player Bounce

            //BitFlags2 - 8
            //1
            //2
            //4
            //8
            //16 - Is used IDK
            //32 - UV Scrolling? (Also This on fences causes issues)
            //64
            //128

            public int SurfaceType; //TrickySurfaceType
            public int CollsionMode; //SSFCollisionMode
            public int CollisonModelIndex;
            public int EffectSlotIndex;

            public int PhysicsIndex;

            public int U8;
        }

        public struct Spline
        {
            public int U1;
            public int RailFlags; //Bit 0 - can be grinded (rail candidacy), toggled at runtime by MainType 25
            public int SplineStyle; //SSFSplineStyle
        }

        public struct EffectHeaderStruct
        {
            public int EffectCount; //Count
            public int EffectOffset; //Offset

            public List<Effect> Effects;
        }

        #region Effects
        public struct Effect
        {
            //Debug Info
            public int Offset;

            public int MainType; //SSFMainType
            public int ByteSize;

            //4 - Wait
            //13 - Reset
            //14 - Multiplyer

            //16 - Sound (ZBxsfx_31)
            //17 - Boost
            //18 - Trick Boost

            //8 - PlaySound
            //7 - Instance Effects
            //21 - Function Run

            //24 - Teleport
            //25 - Spline Effects

            public Type0? type0;
            public Type2? type2;
            public Type3? type3;
            public float WaitTime;
            public Type5? type5;
            public InstanceEffect? Instance;
            public int SoundPlay;
            public Type9? type9;
            public HUDTextEffect? hudTextEffect;
            public float type13; //Reset Value
            public float MultiplierScore;
            public float SpeedBoostDuration; //Seconds the rider's speed limit stays raised
            public float TrickBoostDuration; //Seconds
            public int FunctionRunIndex; //Script Used By Screenlogo
            public int TeleportInstanceIndex;
            public SplineEffect? Spline;
            public byte[]? UnknownPayload;
        }

        #region Type0
        public struct Type0
        {
            public int SubType; //SSFPropertyType

            //5 - DeadNode (Add, Destroy, Remove, Enable)
            //6 - Counter
            //10 - UV Scroll
            //11 - Texture Flipping
            //12 - FenceFlex
            //17 - Crowd
            //23 - Movie?

            public RollerEffect? Roller; //0
            public float Debounce;  //Debounce?
            public int DeadNodeMode; //5 SSFDeadNodeMode
            public CounterEffect? Counter; //6
            public BoostEffect? Boost; //Boost
            public UVScrolling? UVScroll; //10
            public TextureFlipEffect? TextureFlip; //11
            public FenceFlex? Fence; //12
            public FlagEffect? Flag; //13
            public CrackedEffect? Cracked; //14
            public LapBoostEffect? LapBoost; //15
            public CrowdBox? CrowdEffect; //17
            public ZBoostEffect? ZBoost; //18
            public MeshAnimEffect? MeshAnim; //20 cMeshAnim
            public TubeEndBoostEffect? TubeEndBoost; //24
            public AnimObjectEffect? AnimObject; //256
            public AnimDeltaEffect? AnimDelta; //257
            public AnimComboEffect? AnimCombo; //258
        }

        public struct RollerEffect
        {
            public float Mass; //Must be > 0 for the prop to move
            public float U1;
            public float U2;
            public Vector3 LaunchDirection; //All zero uses the way the prop faces
        }

        public struct CounterEffect
        {
            public int Count;
            public float U1;
        }

        public struct BoostEffect
        {
            public int Mode; //SSFBoostMode
            public float Window; //Seconds the node stays armed after it fires
            public float ApproachRate; //Per second, how quickly speed along BoostDir approaches BoostAmount
            public float BoostAmount;
            public Vector3 BoostDir; //World space, not rotated with the prop
        }

        public struct UVScrolling
        {
            public int Mode; //SSFUVScrollMode
            public Vector2 Speed; //UV per 60Hz tick
            public float ActiveDuration; //Seconds
            public float PauseDuration; //Seconds
            public float Lifetime; //Seconds, 0 runs until unloaded
        }

        public struct TextureFlipEffect
        {
            public int StartFrame; //Flipbook frame the flip starts on
            public int Direction;
            public float Speed;
            public float Length;
            public int PauseDwell; //Hold frame 0 and flash frame 1
        }

        public struct FenceFlex
        {
            public int U0;
            public float FlexAmmount;
        }

        public struct FlagEffect
        {
            public int Variant;
            public float Amplitude;
            public float Wavelength;
            public float U3;
        }

        public struct CrackedEffect
        {
            public float CrackLifetime; //Seconds before the crack heals, -1 never
            public float Strength; //Impact budget, runs EffectTriggerSlot when spent
        }

        public struct LapBoostEffect
        {
            public float ApproachRate;
            public float TargetSpeed;
            public Vector3 Axis;
        }

        public struct CrowdBox
        {
            public int U0;
            public int Rows;
            public int Columns;
        }

        public struct ZBoostEffect
        {
            public float ApproachRate;
            public float TargetSpeed;
            public Vector3 Axis;
            public float TargetAltitude; //World Z, only riders below it are lifted
            public float SnapTolerance; //0 eases all the way in
        }

        public struct MeshAnimEffect
        {
            public int U0;
            public float FrameStep; //Seconds
            public float Duration; //Seconds
            public Vector3 ThrowDirection;
            public Vector3 VelocityScale;
            public float DirectionScale;
        }

        public struct TubeEndBoostEffect
        {
            //Inherits the BoostEffect layout, then adds three launch stages
            public int Mode; //SSFBoostMode
            public float Window;
            public float ApproachRate;
            public float TargetSpeed;
            public Vector3 BaseAxis; //All zero in retail
            public Vector3 Stage1Direction;
            public Vector3 Stage2Direction;
            public Vector3 Stage3Direction;
            public float Stage1Speed;
            public float Stage2Speed;
            public float Stage3Speed;
        }

        public struct AnimObjectEffect
        {
            public int LoopMode; //SSFAnimLoopMode
            public float WindowStart; //Frame, negative plays the whole clip
            public float WindowEnd; //Frame, negative plays the whole clip
            public float Rate; //Frames per second, 30 is real time
            public float RandomRateUpper; //0 turns the random rate off
            public float Debounce; //Cooldown before the instance can be triggered again
            public int RandomStart; //Non-zero randomises the starting phase
            public int DirectionMode; //SSFAnimDirectionMode
        }

        public struct AnimDeltaEffect
        {
            //Same as AnimObjectEffect, but frozen until a MainType 3/9 command grants it clip time
            public int LoopMode; //SSFAnimLoopMode
            public float WindowStart;
            public float WindowEnd;
            public float Rate;
            public float RandomRateUpper;
            public float Debounce; //Cooldown before the instance can be triggered again
            public int RandomStart;
            public int DirectionMode; //SSFAnimDirectionMode
        }

        public struct AnimComboEffect
        {
            //The first eight are the idle window, as AnimObjectEffect. The combo window plays over it on command 3
            public int LoopMode; //SSFAnimLoopMode
            public float WindowStart;
            public float WindowEnd;
            public float Rate;
            public float RandomRateUpper;
            public float Debounce; //Cooldown before the instance can be triggered again
            public int RandomStart;
            public int DirectionMode; //SSFAnimDirectionMode
            public float ComboWindowStart; //Negative carries on from the idle window's end
            public float ComboWindowEnd; //Negative runs to the end of the clip
            public float ComboRate;
            public int ComboEndMode; //SSFAnimComboEndMode
        }
        #endregion

        #region Type2
        public struct Type2
        {
            public int SubType; //SSFEmitterType

            public EmitterEffect? Emitter; //0
            public SplinePathAnimation? SplineAnimation; //SplinePath
            public EmitterEffect? CollisionEmitter; //2 CollideEmitter, fired from the contact point
        }

        public struct EmitterEffect
        {
            public int ParticleCount; //Per burst
            public int TrailCopies;
            public float EmissionWindow; //Seconds the starts are staggered over, negative keeps the stream alive
            public float TimeScale;
            public float SizeCenter;
            public float ParticleLifeCenter;
            public float SizeSpan;
            public float ParticleLifeSpan;
            public float TrailSpacing; //Seconds between trail copies
            public Vector3 Origin; //cm from the prop. A collision emitter replaces it with the contact point
            public Vector3 SpawnAxisA;
            public Vector3 SpawnAxisB;
            public Vector3 VelocityBase; //A collision emitter keeps only its length, along the contact normal
            public Vector3 VelocityAxisA;
            public Vector3 VelocityAxisB;
            public Vector3 VelocityAxisC;
            public Vector3 Gravity;
            public Vector4 ColourStop1; //R,G,B,A
            public Vector4 ColourStop2;
            public Vector4 ColourStop3;
            public Vector4 ColourStop4;
            public int SpriteIndex; //Into the shared particle sprite set
            public int BlendMode; //Remapped by the engine before it sets the blend state
        }

        public struct SplinePathAnimation
        {
            public int SplineIndex;
            public int EndMode; //SSFSplineEndMode
            public int OrientationMode; //SSFSplineOrientationMode
            public int InstanceCount;
            public float AnimationSpeed;
            public float YawOffset; //Radians
            public int ShowRouteLine; //Draws the spline as a line, like a gondola cable, in RouteLineColour
            public Vector4 RouteLineColour; //R,G,B,A
        }
        #endregion


        public struct Type3
        {
            //A command for the property node installed on the bound instance. Known commands by receiver:
            //AnimDelta 2 - grant Parameter frames of clip time, AnimCombo 3 - play the combo window
            //TextureFlip 2 - show frame Parameter, UVScroll 6 - jump the V offset to Parameter
            //Counter 1 - mark input Parameter, Counter 3 - count down by one
            //Any 7 - clear instance flag 0x800, Any 8 - set instance flag 0x800
            public int Command;
            public float Parameter;
        }

        public struct Type5
        {
            //When the test fails the chain ends
            public int Mode; //SSFGateMode
            public int Compare; //0 passes at most Threshold, non-zero at least
            public float Threshold;
        }

        public struct InstanceEffect
        {
            public int InstanceIndex;
            public int EffectIndex;
        }
        public struct HUDTextEffect
        {
            public float HudRed;
            public float HudGreen;
            public float HudBlue;

            public string HudText;
        }

        public struct Type9
        {
            //As Type3, without its missing-node guard
            public int Command;
            public float Parameter;
        }

        public struct SplineEffect
        {
            public int SplineIndex;
            public int Effect;

            //0 - Toggle Off
            //1 - Toggle On
        }

        #endregion
    }
}


//sub_type:
//0: # "Roller"?
//    'fff'
//    'iii'
//2: # "Debounce"?
//5: # "DeadNode"? b = 2, 4
//6: # "Counter"
//7: # "Boost" 
//8: # "Timer"
//9: # "Rail"
//10: # "UVScroll" b = 0
//    'ii'
//    'f'
//11: # "TexFlip" #print(f.tell())
//12: # "Fence"
//13: # "Flag"
//14: # "Cracked"
//15: # "LapBoost"
//16: # "RandomBoost"
//17: # "CrowdBox"
//18: # "ZBoost"
//19: # "UVScrollTexFlip"
//20: # "cMeshAnim"
//21: # "TrickTrigger"
//22: # "Particle"
//23: # "Movie"
//24: # "TubeEndBoost"

//256: # "AnimObject"
//257: # "AnimDelta"
//258: # "AnimCombo"
//259: # "AnimTexFlip"

//Unused
//0:
//   8 - "Timer"
//   9 - "Rail"
//  16 - "RandomBoost"
//  19 - "UVScrollTexFlip"
//  21 - "TrickTrigger"
//  22 - "Particle"
//  259 - "AnimTexFlip"
//1: "Camera"
//6: Boost meter fill (showoff modes) (works)
//10: Stubbed
//11: Stubbed
//15: Boost meter fill (showoff modes) (works)
//16: Time bonus(best in showoff) (works)
//23: Camera related(kinda works)
