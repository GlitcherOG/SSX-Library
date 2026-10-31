using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SSXLibrary.Internal.Utilities;
using SSXLibrary.FileHandlers.LevelFiles.Tricky.PS2;

namespace SSXLibrary.JsonFiles.Tricky
{
    public class SSFJsonHandler
    {
        public List<EffectSlotJson> EffectSlots = new List<EffectSlotJson>();
        public List<PhysicsHeader> PhysicsHeaders = new List<PhysicsHeader>();
        public List<Function> Functions = new List<Function>();
        public List<EffectHeaderStruct> EffectHeaders = new List<EffectHeaderStruct>();

        public void CreateJson(string path, bool Inline = false)
        {
            var TempFormating = Formatting.None;
            if (Inline)
            {
                TempFormating = Formatting.Indented;
            }

            var serializer = JsonConvert.SerializeObject(this, TempFormating, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(path, serializer);
        }

        public static SSFJsonHandler Load(string path)
        {
            string paths = path;
            if (File.Exists(paths))
            {
                var stream = File.ReadAllText(paths);
                var container = JsonConvert.DeserializeObject<SSFJsonHandler>(stream);
                return container;
            }
            else
            {
                return new SSFJsonHandler();
            }
        }

        public static Effect EffectToJSON(SSFHandler.Effect TempEffect)
        {
            var NewEffect = new SSFJsonHandler.Effect();
            NewEffect.MainType = TempEffect.MainType;

            if (NewEffect.MainType == 0)
            {
                var NewSubType = new Type0();
                NewSubType.SubType = TempEffect.type0.Value.SubType;
                if (NewSubType.SubType == 0)
                {
                    var NewSubSubType = new RollerEffect();
                    NewSubSubType.Mass = TempEffect.type0.Value.Roller.Value.Mass;
                    NewSubSubType.U1 = TempEffect.type0.Value.Roller.Value.U1;
                    NewSubSubType.U2 = TempEffect.type0.Value.Roller.Value.U2;
                    NewSubSubType.LaunchDirection = ArrayConv.Vector3ToArray(TempEffect.type0.Value.Roller.Value.LaunchDirection);
                    NewSubType.Roller = NewSubSubType;
                }
                else if (NewSubType.SubType == 2)
                {
                    NewSubType.Debounce = TempEffect.type0.Value.Debounce;
                }
                else if (NewSubType.SubType == 5)
                {
                    NewSubType.DeadNodeMode = TempEffect.type0.Value.DeadNodeMode;
                }
                else if (NewSubType.SubType == 6)
                {
                    var NewSubSubType = new CounterEffect();
                    NewSubSubType.Count = TempEffect.type0.Value.Counter.Value.Count;
                    NewSubSubType.U1 = TempEffect.type0.Value.Counter.Value.U1;
                    NewSubType.Counter = NewSubSubType;
                }
                else if (NewSubType.SubType == 7)
                {
                    var NewSubSubType = new BoostEffect();
                    NewSubSubType.Mode = TempEffect.type0.Value.Boost.Value.Mode;
                    NewSubSubType.Window = TempEffect.type0.Value.Boost.Value.Window;
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.Boost.Value.ApproachRate;
                    NewSubSubType.BoostAmount = TempEffect.type0.Value.Boost.Value.BoostAmount;
                    NewSubSubType.BoostDir = ArrayConv.Vector3ToArray(TempEffect.type0.Value.Boost.Value.BoostDir);
                    NewSubType.Boost = NewSubSubType;
                }
                else if (NewSubType.SubType == 10)
                {
                    var NewSubSubType = new UVScrolling();
                    NewSubSubType.Mode = TempEffect.type0.Value.UVScroll.Value.Mode;
                    NewSubSubType.Speed = ArrayConv.Vector2ToArray(TempEffect.type0.Value.UVScroll.Value.Speed);
                    NewSubSubType.ActiveDuration = TempEffect.type0.Value.UVScroll.Value.ActiveDuration;
                    NewSubSubType.PauseDuration = TempEffect.type0.Value.UVScroll.Value.PauseDuration;
                    NewSubSubType.Lifetime = TempEffect.type0.Value.UVScroll.Value.Lifetime;
                    NewSubType.UVScroll = NewSubSubType;
                }
                else if (NewSubType.SubType == 11)
                {
                    var NewSubSubType = new TextureFlipEffect();
                    NewSubSubType.StartFrame = TempEffect.type0.Value.TextureFlip.Value.StartFrame;
                    NewSubSubType.Direction = TempEffect.type0.Value.TextureFlip.Value.Direction;
                    NewSubSubType.Speed = TempEffect.type0.Value.TextureFlip.Value.Speed;
                    NewSubSubType.Length = TempEffect.type0.Value.TextureFlip.Value.Length;
                    NewSubSubType.PauseDwell = TempEffect.type0.Value.TextureFlip.Value.PauseDwell;
                    NewSubType.TextureFlip = NewSubSubType;
                }
                else if (NewSubType.SubType == 12)
                {
                    var NewSubSubType = new FenceFlex();
                    NewSubSubType.U0 = TempEffect.type0.Value.Fence.Value.U0;
                    NewSubSubType.FlexAmmount = TempEffect.type0.Value.Fence.Value.FlexAmmount;
                    NewSubType.Fence = NewSubSubType;
                }
                else if (NewSubType.SubType == 13)
                {
                    var NewSubSubType = new FlagEffect();
                    NewSubSubType.Variant = TempEffect.type0.Value.Flag.Value.Variant;
                    NewSubSubType.Amplitude = TempEffect.type0.Value.Flag.Value.Amplitude;
                    NewSubSubType.Wavelength = TempEffect.type0.Value.Flag.Value.Wavelength;
                    NewSubSubType.U3 = TempEffect.type0.Value.Flag.Value.U3;
                    NewSubType.Flag = NewSubSubType;
                }
                else if (NewSubType.SubType == 14)
                {
                    var NewSubSubType = new CrackedEffect();
                    NewSubSubType.CrackLifetime = TempEffect.type0.Value.Cracked.Value.CrackLifetime;
                    NewSubSubType.Strength = TempEffect.type0.Value.Cracked.Value.Strength;
                    NewSubType.Cracked = NewSubSubType;
                }
                else if (NewSubType.SubType == 15)
                {
                    var NewSubSubType = new LapBoostEffect();
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.LapBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.LapBoost.Value.TargetSpeed;
                    NewSubSubType.Axis = ArrayConv.Vector3ToArray(TempEffect.type0.Value.LapBoost.Value.Axis);
                    NewSubType.LapBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 17)
                {
                    var NewSubSubType = new CrowdBox();
                    NewSubSubType.U0 = TempEffect.type0.Value.CrowdEffect.Value.U0;
                    NewSubSubType.Rows = TempEffect.type0.Value.CrowdEffect.Value.Rows;
                    NewSubSubType.Columns = TempEffect.type0.Value.CrowdEffect.Value.Columns;
                    NewSubType.CrowdEffect = NewSubSubType;
                }
                else if (NewSubType.SubType == 18)
                {
                    var NewSubSubType = new ZBoostEffect();
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.ZBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.ZBoost.Value.TargetSpeed;
                    NewSubSubType.Axis = ArrayConv.Vector3ToArray(TempEffect.type0.Value.ZBoost.Value.Axis);
                    NewSubSubType.TargetAltitude = TempEffect.type0.Value.ZBoost.Value.TargetAltitude;
                    NewSubSubType.SnapTolerance = TempEffect.type0.Value.ZBoost.Value.SnapTolerance;
                    NewSubType.ZBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 20)
                {
                    var NewSubSubType = new MeshAnimEffect();
                    NewSubSubType.U0 = TempEffect.type0.Value.MeshAnim.Value.U0;
                    NewSubSubType.FrameStep = TempEffect.type0.Value.MeshAnim.Value.FrameStep;
                    NewSubSubType.Duration = TempEffect.type0.Value.MeshAnim.Value.Duration;
                    NewSubSubType.ThrowDirection = ArrayConv.Vector3ToArray(TempEffect.type0.Value.MeshAnim.Value.ThrowDirection);
                    NewSubSubType.VelocityScale = ArrayConv.Vector3ToArray(TempEffect.type0.Value.MeshAnim.Value.VelocityScale);
                    NewSubSubType.DirectionScale = TempEffect.type0.Value.MeshAnim.Value.DirectionScale;
                    NewSubType.MeshAnim = NewSubSubType;
                }
                else if (NewSubType.SubType == 23)
                {
                    //DONE
                }
                else if (NewSubType.SubType == 24)
                {
                    var NewSubSubType = new TubeEndBoostEffect();
                    NewSubSubType.Mode = TempEffect.type0.Value.TubeEndBoost.Value.Mode;
                    NewSubSubType.Window = TempEffect.type0.Value.TubeEndBoost.Value.Window;
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.TubeEndBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.TubeEndBoost.Value.TargetSpeed;
                    NewSubSubType.BaseAxis = ArrayConv.Vector3ToArray(TempEffect.type0.Value.TubeEndBoost.Value.BaseAxis);
                    NewSubSubType.Stage1Direction = ArrayConv.Vector3ToArray(TempEffect.type0.Value.TubeEndBoost.Value.Stage1Direction);
                    NewSubSubType.Stage2Direction = ArrayConv.Vector3ToArray(TempEffect.type0.Value.TubeEndBoost.Value.Stage2Direction);
                    NewSubSubType.Stage3Direction = ArrayConv.Vector3ToArray(TempEffect.type0.Value.TubeEndBoost.Value.Stage3Direction);
                    NewSubSubType.Stage1Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage1Speed;
                    NewSubSubType.Stage2Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage2Speed;
                    NewSubSubType.Stage3Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage3Speed;
                    NewSubType.TubeEndBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 256)
                {
                    var NewSubSubType = new AnimObjectEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimObject.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimObject.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimObject.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimObject.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimObject.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimObject.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimObject.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimObject.Value.DirectionMode;
                    NewSubType.AnimObject = NewSubSubType;
                }
                else if (NewSubType.SubType == 257)
                {
                    var NewSubSubType = new AnimDeltaEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimDelta.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimDelta.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimDelta.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimDelta.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimDelta.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimDelta.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimDelta.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimDelta.Value.DirectionMode;
                    NewSubType.AnimDelta = NewSubSubType;
                }
                else if (NewSubType.SubType == 258)
                {
                    var NewSubSubType = new AnimComboEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimCombo.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimCombo.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimCombo.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimCombo.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimCombo.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimCombo.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimCombo.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimCombo.Value.DirectionMode;
                    NewSubSubType.ComboWindowStart = TempEffect.type0.Value.AnimCombo.Value.ComboWindowStart;
                    NewSubSubType.ComboWindowEnd = TempEffect.type0.Value.AnimCombo.Value.ComboWindowEnd;
                    NewSubSubType.ComboRate = TempEffect.type0.Value.AnimCombo.Value.ComboRate;
                    NewSubSubType.ComboEndMode = TempEffect.type0.Value.AnimCombo.Value.ComboEndMode;
                    NewSubType.AnimCombo = NewSubSubType;
                }
                else
                {
                    //MessageBox.Show("ERROR Converting to Json Format SSF " + NewEffect.MainType + ", " + NewEffect.type0.Value.SubType);
                }

                NewEffect.type0 = NewSubType;
            }
            else if (NewEffect.MainType == 2)
            {
                var NewSubType = new Type2();
                NewSubType.SubType = TempEffect.type2.Value.SubType;

                if (NewSubType.SubType == 0)
                {
                    NewSubType.Emitter = EmitterToJSON(TempEffect.type2.Value.Emitter.Value);
                }
                else if (NewSubType.SubType == 1)
                {
                    var NewSubSubType = new SplinePathAnimation();
                    NewSubSubType.SplineIndex = TempEffect.type2.Value.SplineAnimation.Value.SplineIndex;
                    NewSubSubType.EndMode = TempEffect.type2.Value.SplineAnimation.Value.EndMode;
                    NewSubSubType.OrientationMode = TempEffect.type2.Value.SplineAnimation.Value.OrientationMode;
                    NewSubSubType.InstanceCount = TempEffect.type2.Value.SplineAnimation.Value.InstanceCount;
                    NewSubSubType.AnimationSpeed = TempEffect.type2.Value.SplineAnimation.Value.AnimationSpeed;
                    NewSubSubType.YawOffset = TempEffect.type2.Value.SplineAnimation.Value.YawOffset;
                    NewSubSubType.ShowRouteLine = TempEffect.type2.Value.SplineAnimation.Value.ShowRouteLine;
                    NewSubSubType.RouteLineColour = ArrayConv.Vector4ToArray(TempEffect.type2.Value.SplineAnimation.Value.RouteLineColour);

                    NewSubType.SplineAnimation = NewSubSubType;
                }
                else if (NewSubType.SubType == 2)
                {
                    NewSubType.CollisionEmitter = EmitterToJSON(TempEffect.type2.Value.CollisionEmitter.Value);
                }
                else
                {
                    //MessageBox.Show("ERROR Converting to Json Format SSF " + NewEffect.MainType + ", " + NewEffect.type2.Value.SubType);
                }

                NewEffect.type2 = NewSubType;
            }
            else if (NewEffect.MainType == 3)
            {
                var NewSubType = new Type3();

                NewSubType.Command = TempEffect.type3.Value.Command;
                NewSubType.Parameter = TempEffect.type3.Value.Parameter;

                NewEffect.type3 = NewSubType;
            }
            else if (NewEffect.MainType == 4)
            {
                NewEffect.WaitTime = TempEffect.WaitTime;
            }
            else if (NewEffect.MainType == 5)
            {
                var NewSubType = new Type5();

                NewSubType.Mode = TempEffect.type5.Value.Mode;
                NewSubType.Compare = TempEffect.type5.Value.Compare;
                NewSubType.Threshold = TempEffect.type5.Value.Threshold;

                NewEffect.type5 = NewSubType;
            }
            else if (NewEffect.MainType == 7)
            {
                var NewInstance = new InstanceEffect();

                NewInstance.InstanceIndex = TempEffect.Instance.Value.InstanceIndex;
                NewInstance.EffectIndex = TempEffect.Instance.Value.EffectIndex;

                NewEffect.Instance = NewInstance;
            }
            else if (NewEffect.MainType == 8)
            {
                NewEffect.SoundPlay = TempEffect.SoundPlay;
            }
            else if (NewEffect.MainType == 9)
            {
                var NewSubType = new Type9();

                NewSubType.Command = TempEffect.type9.Value.Command;
                NewSubType.Parameter = TempEffect.type9.Value.Parameter;

                NewEffect.type9 = NewSubType;
            }
            else if (NewEffect.MainType == 13)
            {
                NewEffect.type13 = TempEffect.type13;
            }
            else if (NewEffect.MainType == 14)
            {
                NewEffect.MultiplierScore = TempEffect.MultiplierScore;
            }
            else if (NewEffect.MainType == 17)
            {
                NewEffect.SpeedBoostDuration = TempEffect.SpeedBoostDuration;
            }
            else if (NewEffect.MainType == 18)
            {
                NewEffect.TrickBoostDuration = TempEffect.TrickBoostDuration;
            }
            else if (NewEffect.MainType == 21)
            {
                NewEffect.FunctionRunIndex = TempEffect.FunctionRunIndex;
            }
            else if (NewEffect.MainType == 24)
            {
                NewEffect.TeleportInstanceIndex = TempEffect.TeleportInstanceIndex;
            }
            else if (NewEffect.MainType == 25)
            {
                var NewSpline = new SplineEffect();

                NewSpline.SplineIndex = TempEffect.Spline.Value.SplineIndex;
                NewSpline.Effect = TempEffect.Spline.Value.Effect;

                NewEffect.Spline = NewSpline;
            }
            else if (NewEffect.MainType == 12)
            {
                var NewHudEffect = new HUDTextEffect();

                NewHudEffect.HudText = TempEffect.hudTextEffect.Value.HudText;
                NewHudEffect.HudRed = TempEffect.hudTextEffect.Value.HudRed;
                NewHudEffect.HudGreen = TempEffect.hudTextEffect.Value.HudGreen;
                NewHudEffect.HudBlue = TempEffect.hudTextEffect.Value.HudBlue;

                NewEffect.hudTextEffect = NewHudEffect;
            }
            else
            {
                //Carry an unrecognised opcode's bytes through the JSON rather than dropping it, so
                //the document round trip is as lossless as the binary one.
                NewEffect.UnknownPayload = TempEffect.UnknownPayload;
            }

            return NewEffect;
        }

        public static SSFHandler.Effect JSONToEffect(Effect TempEffect)
        {
            var NewEffect = new SSFHandler.Effect();
            NewEffect.MainType = TempEffect.MainType;

            if (NewEffect.MainType == 0)
            {
                var NewSubType = new SSFHandler.Type0();
                NewSubType.SubType = TempEffect.type0.Value.SubType;
                if (NewSubType.SubType == 0)
                {
                    var NewSubSubType = new SSFHandler.RollerEffect();
                    NewSubSubType.Mass = TempEffect.type0.Value.Roller.Value.Mass;
                    NewSubSubType.U1 = TempEffect.type0.Value.Roller.Value.U1;
                    NewSubSubType.U2 = TempEffect.type0.Value.Roller.Value.U2;
                    NewSubSubType.LaunchDirection = ArrayConv.ArrayToVector3(TempEffect.type0.Value.Roller.Value.LaunchDirection);
                    NewSubType.Roller = NewSubSubType;
                }
                else if (NewSubType.SubType == 2)
                {
                    NewSubType.Debounce = TempEffect.type0.Value.Debounce.Value;
                }
                else if (NewSubType.SubType == 5)
                {
                    NewSubType.DeadNodeMode = TempEffect.type0.Value.DeadNodeMode.Value;
                }
                else if (NewSubType.SubType == 6)
                {
                    var NewSubSubType = new SSFHandler.CounterEffect();
                    NewSubSubType.Count = TempEffect.type0.Value.Counter.Value.Count;
                    NewSubSubType.U1 = TempEffect.type0.Value.Counter.Value.U1;
                    NewSubType.Counter = NewSubSubType;
                }
                else if (NewSubType.SubType == 7)
                {
                    var NewSubSubType = new SSFHandler.BoostEffect();
                    NewSubSubType.Mode = TempEffect.type0.Value.Boost.Value.Mode;
                    NewSubSubType.Window = TempEffect.type0.Value.Boost.Value.Window;
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.Boost.Value.ApproachRate;
                    NewSubSubType.BoostAmount = TempEffect.type0.Value.Boost.Value.BoostAmount;
                    NewSubSubType.BoostDir = ArrayConv.ArrayToVector3(TempEffect.type0.Value.Boost.Value.BoostDir);
                    NewSubType.Boost = NewSubSubType;
                }
                else if (NewSubType.SubType == 10)
                {
                    var NewSubSubType = new SSFHandler.UVScrolling();
                    NewSubSubType.Mode = TempEffect.type0.Value.UVScroll.Value.Mode;
                    NewSubSubType.Speed = ArrayConv.ArrayToVector2(TempEffect.type0.Value.UVScroll.Value.Speed);
                    NewSubSubType.ActiveDuration = TempEffect.type0.Value.UVScroll.Value.ActiveDuration;
                    NewSubSubType.PauseDuration = TempEffect.type0.Value.UVScroll.Value.PauseDuration;
                    NewSubSubType.Lifetime = TempEffect.type0.Value.UVScroll.Value.Lifetime;
                    NewSubType.UVScroll = NewSubSubType;
                }
                else if (NewSubType.SubType == 11)
                {
                    var NewSubSubType = new SSFHandler.TextureFlipEffect();
                    NewSubSubType.StartFrame = TempEffect.type0.Value.TextureFlip.Value.StartFrame;
                    NewSubSubType.Direction = TempEffect.type0.Value.TextureFlip.Value.Direction;
                    NewSubSubType.Speed = TempEffect.type0.Value.TextureFlip.Value.Speed;
                    NewSubSubType.Length = TempEffect.type0.Value.TextureFlip.Value.Length;
                    NewSubSubType.PauseDwell = TempEffect.type0.Value.TextureFlip.Value.PauseDwell;
                    NewSubType.TextureFlip = NewSubSubType;
                }
                else if (NewSubType.SubType == 12)
                {
                    var NewSubSubType = new SSFHandler.FenceFlex();
                    NewSubSubType.U0 = TempEffect.type0.Value.Fence.Value.U0;
                    NewSubSubType.FlexAmmount = TempEffect.type0.Value.Fence.Value.FlexAmmount;
                    NewSubType.Fence = NewSubSubType;
                }
                else if (NewSubType.SubType == 13)
                {
                    var NewSubSubType = new SSFHandler.FlagEffect();
                    NewSubSubType.Variant = TempEffect.type0.Value.Flag.Value.Variant;
                    NewSubSubType.Amplitude = TempEffect.type0.Value.Flag.Value.Amplitude;
                    NewSubSubType.Wavelength = TempEffect.type0.Value.Flag.Value.Wavelength;
                    NewSubSubType.U3 = TempEffect.type0.Value.Flag.Value.U3;
                    NewSubType.Flag = NewSubSubType;
                }
                else if (NewSubType.SubType == 14)
                {
                    var NewSubSubType = new SSFHandler.CrackedEffect();
                    NewSubSubType.CrackLifetime = TempEffect.type0.Value.Cracked.Value.CrackLifetime;
                    NewSubSubType.Strength = TempEffect.type0.Value.Cracked.Value.Strength;
                    NewSubType.Cracked = NewSubSubType;
                }
                else if (NewSubType.SubType == 15)
                {
                    var NewSubSubType = new SSFHandler.LapBoostEffect();
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.LapBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.LapBoost.Value.TargetSpeed;
                    NewSubSubType.Axis = ArrayConv.ArrayToVector3(TempEffect.type0.Value.LapBoost.Value.Axis);
                    NewSubType.LapBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 17)
                {
                    var NewSubSubType = new SSFHandler.CrowdBox();
                    NewSubSubType.U0 = TempEffect.type0.Value.CrowdEffect.Value.U0;
                    NewSubSubType.Rows = TempEffect.type0.Value.CrowdEffect.Value.Rows;
                    NewSubSubType.Columns = TempEffect.type0.Value.CrowdEffect.Value.Columns;
                    NewSubType.CrowdEffect = NewSubSubType;
                }
                else if (NewSubType.SubType == 18)
                {
                    var NewSubSubType = new SSFHandler.ZBoostEffect();
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.ZBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.ZBoost.Value.TargetSpeed;
                    NewSubSubType.Axis = ArrayConv.ArrayToVector3(TempEffect.type0.Value.ZBoost.Value.Axis);
                    NewSubSubType.TargetAltitude = TempEffect.type0.Value.ZBoost.Value.TargetAltitude;
                    NewSubSubType.SnapTolerance = TempEffect.type0.Value.ZBoost.Value.SnapTolerance;
                    NewSubType.ZBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 20)
                {
                    var NewSubSubType = new SSFHandler.MeshAnimEffect();
                    NewSubSubType.U0 = TempEffect.type0.Value.MeshAnim.Value.U0;
                    NewSubSubType.FrameStep = TempEffect.type0.Value.MeshAnim.Value.FrameStep;
                    NewSubSubType.Duration = TempEffect.type0.Value.MeshAnim.Value.Duration;
                    NewSubSubType.ThrowDirection = ArrayConv.ArrayToVector3(TempEffect.type0.Value.MeshAnim.Value.ThrowDirection);
                    NewSubSubType.VelocityScale = ArrayConv.ArrayToVector3(TempEffect.type0.Value.MeshAnim.Value.VelocityScale);
                    NewSubSubType.DirectionScale = TempEffect.type0.Value.MeshAnim.Value.DirectionScale;
                    NewSubType.MeshAnim = NewSubSubType;
                }
                else if (NewSubType.SubType == 23)
                {
                    //DONE
                }
                else if (NewSubType.SubType == 24)
                {
                    var NewSubSubType = new SSFHandler.TubeEndBoostEffect();
                    NewSubSubType.Mode = TempEffect.type0.Value.TubeEndBoost.Value.Mode;
                    NewSubSubType.Window = TempEffect.type0.Value.TubeEndBoost.Value.Window;
                    NewSubSubType.ApproachRate = TempEffect.type0.Value.TubeEndBoost.Value.ApproachRate;
                    NewSubSubType.TargetSpeed = TempEffect.type0.Value.TubeEndBoost.Value.TargetSpeed;
                    NewSubSubType.BaseAxis = ArrayConv.ArrayToVector3(TempEffect.type0.Value.TubeEndBoost.Value.BaseAxis);
                    NewSubSubType.Stage1Direction = ArrayConv.ArrayToVector3(TempEffect.type0.Value.TubeEndBoost.Value.Stage1Direction);
                    NewSubSubType.Stage2Direction = ArrayConv.ArrayToVector3(TempEffect.type0.Value.TubeEndBoost.Value.Stage2Direction);
                    NewSubSubType.Stage3Direction = ArrayConv.ArrayToVector3(TempEffect.type0.Value.TubeEndBoost.Value.Stage3Direction);
                    NewSubSubType.Stage1Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage1Speed;
                    NewSubSubType.Stage2Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage2Speed;
                    NewSubSubType.Stage3Speed = TempEffect.type0.Value.TubeEndBoost.Value.Stage3Speed;
                    NewSubType.TubeEndBoost = NewSubSubType;
                }
                else if (NewSubType.SubType == 256)
                {
                    var NewSubSubType = new SSFHandler.AnimObjectEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimObject.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimObject.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimObject.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimObject.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimObject.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimObject.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimObject.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimObject.Value.DirectionMode;
                    NewSubType.AnimObject = NewSubSubType;
                }
                else if (NewSubType.SubType == 257)
                {
                    var NewSubSubType = new SSFHandler.AnimDeltaEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimDelta.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimDelta.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimDelta.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimDelta.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimDelta.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimDelta.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimDelta.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimDelta.Value.DirectionMode;
                    NewSubType.AnimDelta = NewSubSubType;
                }
                else if (NewSubType.SubType == 258)
                {
                    var NewSubSubType = new SSFHandler.AnimComboEffect();
                    NewSubSubType.LoopMode = TempEffect.type0.Value.AnimCombo.Value.LoopMode;
                    NewSubSubType.WindowStart = TempEffect.type0.Value.AnimCombo.Value.WindowStart;
                    NewSubSubType.WindowEnd = TempEffect.type0.Value.AnimCombo.Value.WindowEnd;
                    NewSubSubType.Rate = TempEffect.type0.Value.AnimCombo.Value.Rate;
                    NewSubSubType.RandomRateUpper = TempEffect.type0.Value.AnimCombo.Value.RandomRateUpper;
                    NewSubSubType.Debounce = TempEffect.type0.Value.AnimCombo.Value.Debounce;
                    NewSubSubType.RandomStart = TempEffect.type0.Value.AnimCombo.Value.RandomStart;
                    NewSubSubType.DirectionMode = TempEffect.type0.Value.AnimCombo.Value.DirectionMode;
                    NewSubSubType.ComboWindowStart = TempEffect.type0.Value.AnimCombo.Value.ComboWindowStart;
                    NewSubSubType.ComboWindowEnd = TempEffect.type0.Value.AnimCombo.Value.ComboWindowEnd;
                    NewSubSubType.ComboRate = TempEffect.type0.Value.AnimCombo.Value.ComboRate;
                    NewSubSubType.ComboEndMode = TempEffect.type0.Value.AnimCombo.Value.ComboEndMode;
                    NewSubType.AnimCombo = NewSubSubType;
                }
                else
                {
                    //MessageBox.Show("ERROR Converting to Json Format SSF " + NewEffect.MainType + ", " + NewEffect.type0.Value.SubType);
                }

                NewEffect.type0 = NewSubType;
            }
            else if (NewEffect.MainType == 2)
            {
                var NewSubType = new SSFHandler.Type2();
                NewSubType.SubType = TempEffect.type2.Value.SubType;

                if (NewSubType.SubType == 0)
                {
                    NewSubType.Emitter = JSONToEmitter(TempEffect.type2.Value.Emitter.Value);
                }
                else if (NewSubType.SubType == 1)
                {
                    var NewSubSubType = new SSFHandler.SplinePathAnimation();
                    NewSubSubType.SplineIndex = TempEffect.type2.Value.SplineAnimation.Value.SplineIndex;
                    NewSubSubType.EndMode = TempEffect.type2.Value.SplineAnimation.Value.EndMode;
                    NewSubSubType.OrientationMode = TempEffect.type2.Value.SplineAnimation.Value.OrientationMode;
                    NewSubSubType.InstanceCount = TempEffect.type2.Value.SplineAnimation.Value.InstanceCount;
                    NewSubSubType.AnimationSpeed = TempEffect.type2.Value.SplineAnimation.Value.AnimationSpeed;
                    NewSubSubType.YawOffset = TempEffect.type2.Value.SplineAnimation.Value.YawOffset;
                    NewSubSubType.ShowRouteLine = TempEffect.type2.Value.SplineAnimation.Value.ShowRouteLine;
                    NewSubSubType.RouteLineColour = ArrayConv.ArrayToVector4(TempEffect.type2.Value.SplineAnimation.Value.RouteLineColour);

                    NewSubType.SplineAnimation = NewSubSubType;
                }
                else if (NewSubType.SubType == 2)
                {
                    NewSubType.CollisionEmitter = JSONToEmitter(TempEffect.type2.Value.CollisionEmitter.Value);
                }
                else
                {
                    //MessageBox.Show("ERROR Converting to Json Format SSF " + NewEffect.MainType + ", " + NewEffect.type2.Value.SubType);
                }

                NewEffect.type2 = NewSubType;
            }
            else if (NewEffect.MainType == 3)
            {
                var NewSubType = new SSFHandler.Type3();

                NewSubType.Command = TempEffect.type3.Value.Command;
                NewSubType.Parameter = TempEffect.type3.Value.Parameter;

                NewEffect.type3 = NewSubType;
            }
            else if (NewEffect.MainType == 4)
            {
                NewEffect.WaitTime = TempEffect.WaitTime.Value;
            }
            else if (NewEffect.MainType == 5)
            {
                var NewSubType = new SSFHandler.Type5();

                NewSubType.Mode = TempEffect.type5.Value.Mode;
                NewSubType.Compare = TempEffect.type5.Value.Compare;
                NewSubType.Threshold = TempEffect.type5.Value.Threshold;

                NewEffect.type5 = NewSubType;
            }
            else if (NewEffect.MainType == 7)
            {
                var NewInstance = new SSFHandler.InstanceEffect();

                NewInstance.InstanceIndex = TempEffect.Instance.Value.InstanceIndex;
                NewInstance.EffectIndex = TempEffect.Instance.Value.EffectIndex;

                NewEffect.Instance = NewInstance;
            }
            else if (NewEffect.MainType == 8)
            {
                NewEffect.SoundPlay = TempEffect.SoundPlay.Value;
            }
            else if (NewEffect.MainType == 9)
            {
                var NewSubType = new SSFHandler.Type9();

                NewSubType.Command = TempEffect.type9.Value.Command;
                NewSubType.Parameter = TempEffect.type9.Value.Parameter;

                NewEffect.type9 = NewSubType;
            }
            else if (NewEffect.MainType == 13)
            {
                NewEffect.type13 = TempEffect.type13.Value;
            }
            else if (NewEffect.MainType == 14)
            {
                NewEffect.MultiplierScore = TempEffect.MultiplierScore.Value;
            }
            else if (NewEffect.MainType == 17)
            {
                NewEffect.SpeedBoostDuration = TempEffect.SpeedBoostDuration.Value;
            }
            else if (NewEffect.MainType == 18)
            {
                NewEffect.TrickBoostDuration = TempEffect.TrickBoostDuration.Value;
            }
            else if (NewEffect.MainType == 21)
            {
                NewEffect.FunctionRunIndex = TempEffect.FunctionRunIndex.Value;
            }
            else if (NewEffect.MainType == 24)
            {
                NewEffect.TeleportInstanceIndex = TempEffect.TeleportInstanceIndex.Value;
            }
            else if (NewEffect.MainType == 25)
            {
                var NewSpline = new SSFHandler.SplineEffect();

                NewSpline.SplineIndex = TempEffect.Spline.Value.SplineIndex;
                NewSpline.Effect = TempEffect.Spline.Value.Effect;

                NewEffect.Spline = NewSpline;
            }
            else if (NewEffect.MainType == 12)
            {
                var NewHUDEffect = new SSFHandler.HUDTextEffect();

                NewHUDEffect.HudText = TempEffect.hudTextEffect.Value.HudText;
                //A document that names no colour gets white, which is what the banner draws anyway.
                NewHUDEffect.HudRed = TempEffect.hudTextEffect.Value.HudRed;
                NewHUDEffect.HudGreen = TempEffect.hudTextEffect.Value.HudGreen;
                NewHUDEffect.HudBlue = TempEffect.hudTextEffect.Value.HudBlue;

                NewEffect.hudTextEffect = NewHUDEffect;
            }
            else
            {
                NewEffect.UnknownPayload = TempEffect.UnknownPayload;
            }

            return NewEffect;
        }

        static EmitterEffect EmitterToJSON(SSFHandler.EmitterEffect TempEmitter)
        {
            var NewEmitter = new EmitterEffect();
            NewEmitter.ParticleCount = TempEmitter.ParticleCount;
            NewEmitter.TrailCopies = TempEmitter.TrailCopies;
            NewEmitter.EmissionWindow = TempEmitter.EmissionWindow;
            NewEmitter.TimeScale = TempEmitter.TimeScale;
            NewEmitter.SizeCenter = TempEmitter.SizeCenter;
            NewEmitter.ParticleLifeCenter = TempEmitter.ParticleLifeCenter;
            NewEmitter.SizeSpan = TempEmitter.SizeSpan;
            NewEmitter.ParticleLifeSpan = TempEmitter.ParticleLifeSpan;
            NewEmitter.TrailSpacing = TempEmitter.TrailSpacing;
            NewEmitter.Origin = ArrayConv.Vector3ToArray(TempEmitter.Origin);
            NewEmitter.SpawnAxisA = ArrayConv.Vector3ToArray(TempEmitter.SpawnAxisA);
            NewEmitter.SpawnAxisB = ArrayConv.Vector3ToArray(TempEmitter.SpawnAxisB);
            NewEmitter.VelocityBase = ArrayConv.Vector3ToArray(TempEmitter.VelocityBase);
            NewEmitter.VelocityAxisA = ArrayConv.Vector3ToArray(TempEmitter.VelocityAxisA);
            NewEmitter.VelocityAxisB = ArrayConv.Vector3ToArray(TempEmitter.VelocityAxisB);
            NewEmitter.VelocityAxisC = ArrayConv.Vector3ToArray(TempEmitter.VelocityAxisC);
            NewEmitter.Gravity = ArrayConv.Vector3ToArray(TempEmitter.Gravity);
            NewEmitter.ColourStop1 = ArrayConv.Vector4ToArray(TempEmitter.ColourStop1);
            NewEmitter.ColourStop2 = ArrayConv.Vector4ToArray(TempEmitter.ColourStop2);
            NewEmitter.ColourStop3 = ArrayConv.Vector4ToArray(TempEmitter.ColourStop3);
            NewEmitter.ColourStop4 = ArrayConv.Vector4ToArray(TempEmitter.ColourStop4);
            NewEmitter.SpriteIndex = TempEmitter.SpriteIndex;
            NewEmitter.BlendMode = TempEmitter.BlendMode;
            return NewEmitter;
        }

        static SSFHandler.EmitterEffect JSONToEmitter(EmitterEffect TempEmitter)
        {
            var NewEmitter = new SSFHandler.EmitterEffect();
            NewEmitter.ParticleCount = TempEmitter.ParticleCount;
            NewEmitter.TrailCopies = TempEmitter.TrailCopies;
            NewEmitter.EmissionWindow = TempEmitter.EmissionWindow;
            NewEmitter.TimeScale = TempEmitter.TimeScale;
            NewEmitter.SizeCenter = TempEmitter.SizeCenter;
            NewEmitter.ParticleLifeCenter = TempEmitter.ParticleLifeCenter;
            NewEmitter.SizeSpan = TempEmitter.SizeSpan;
            NewEmitter.ParticleLifeSpan = TempEmitter.ParticleLifeSpan;
            NewEmitter.TrailSpacing = TempEmitter.TrailSpacing;
            NewEmitter.Origin = ArrayConv.ArrayToVector3(TempEmitter.Origin);
            NewEmitter.SpawnAxisA = ArrayConv.ArrayToVector3(TempEmitter.SpawnAxisA);
            NewEmitter.SpawnAxisB = ArrayConv.ArrayToVector3(TempEmitter.SpawnAxisB);
            NewEmitter.VelocityBase = ArrayConv.ArrayToVector3(TempEmitter.VelocityBase);
            NewEmitter.VelocityAxisA = ArrayConv.ArrayToVector3(TempEmitter.VelocityAxisA);
            NewEmitter.VelocityAxisB = ArrayConv.ArrayToVector3(TempEmitter.VelocityAxisB);
            NewEmitter.VelocityAxisC = ArrayConv.ArrayToVector3(TempEmitter.VelocityAxisC);
            NewEmitter.Gravity = ArrayConv.ArrayToVector3(TempEmitter.Gravity);
            NewEmitter.ColourStop1 = ArrayConv.ArrayToVector4(TempEmitter.ColourStop1);
            NewEmitter.ColourStop2 = ArrayConv.ArrayToVector4(TempEmitter.ColourStop2);
            NewEmitter.ColourStop3 = ArrayConv.ArrayToVector4(TempEmitter.ColourStop3);
            NewEmitter.ColourStop4 = ArrayConv.ArrayToVector4(TempEmitter.ColourStop4);
            NewEmitter.SpriteIndex = TempEmitter.SpriteIndex;
            NewEmitter.BlendMode = TempEmitter.BlendMode;
            return NewEmitter;
        }

        public struct EffectSlotJson
        {
            public string EffectSlotName;

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
            public string PhysicsName;
            public List<PhysicsData> PhysicsDatas;
        }

        //Field notes and value enums (TrickyEnums.cs) are on the matching SSFHandler structs
        public struct PhysicsData
        {
            public int OccupancyMaskEncoding;

            public float[] Center;
            public float UFloat3;
            public float UFloat4;
            public float UFloat5;
            public float InertiaM11;
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

            public List<OccupancyLevel> OccupancyLevels;

            public byte[] OccupancyMasks;
        }

        public struct OccupancyLevel
        {
            public float SphereRadius;
            public float ChildOffset;
            public int MaskStride;
        }

        public struct EffectHeaderStruct
        {
            public string EffectName;
            public List<Effect> Effects;
        }

        public struct Function
        {
            public string FunctionName;
            public List<Effect> Effects;
        }

        #region Effects
        public struct Effect
        {
            public int MainType; //SSFMainType

            public Type0? type0;
            public Type2? type2;
            public Type3? type3;
            public float? WaitTime;
            public Type5? type5;
            public InstanceEffect? Instance;
            public int? SoundPlay;
            public Type9? type9;

            public HUDTextEffect? hudTextEffect;
            public float? type13;
            public float? MultiplierScore;
            public float? SpeedBoostDuration;
            public float? TrickBoostDuration;
            public int? FunctionRunIndex;
            public int? TeleportInstanceIndex;
            public SplineEffect? Spline;

            public byte[]? UnknownPayload;     //any main type this library has no branch for
        }

        #region Type0
        public struct Type0
        {
            public int SubType;

            public RollerEffect? Roller; //0
            public float? Debounce;  //Debounce?
            public int? DeadNodeMode; //5
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
            public float Mass;
            public float U1;
            public float U2;
            public float[] LaunchDirection;
        }

        public struct CounterEffect
        {
            public int Count;
            public float U1;
        }

        public struct BoostEffect
        {
            public int Mode;
            public float Window;
            public float ApproachRate;
            public float BoostAmount;
            public float[] BoostDir;
        }

        public struct UVScrolling
        {
            public int Mode;
            public float[] Speed;
            public float ActiveDuration;
            public float PauseDuration;
            public float Lifetime;
        }

        public struct TextureFlipEffect
        {
            public int StartFrame;
            public int Direction;
            public float Speed;
            public float Length;
            public int PauseDwell;
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
            public float CrackLifetime;
            public float Strength;
        }

        public struct LapBoostEffect
        {
            public float ApproachRate;
            public float TargetSpeed;
            public float[] Axis;
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
            public float[] Axis;
            public float TargetAltitude;
            public float SnapTolerance;
        }

        public struct MeshAnimEffect
        {
            public int U0;
            public float FrameStep;
            public float Duration;
            public float[] ThrowDirection;
            public float[] VelocityScale;
            public float DirectionScale;
        }

        public struct TubeEndBoostEffect
        {
            public int Mode;
            public float Window;
            public float ApproachRate;
            public float TargetSpeed;
            public float[] BaseAxis;
            public float[] Stage1Direction;
            public float[] Stage2Direction;
            public float[] Stage3Direction;
            public float Stage1Speed;
            public float Stage2Speed;
            public float Stage3Speed;
        }

        public struct AnimObjectEffect
        {
            public int LoopMode;
            public float WindowStart;
            public float WindowEnd;
            public float Rate;
            public float RandomRateUpper;
            public float Debounce;
            public int RandomStart;
            public int DirectionMode;
        }

        public struct AnimDeltaEffect
        {
            public int LoopMode;
            public float WindowStart;
            public float WindowEnd;
            public float Rate;
            public float RandomRateUpper;
            public float Debounce;
            public int RandomStart;
            public int DirectionMode;
        }

        public struct AnimComboEffect
        {
            public int LoopMode;
            public float WindowStart;
            public float WindowEnd;
            public float Rate;
            public float RandomRateUpper;
            public float Debounce;
            public int RandomStart;
            public int DirectionMode;
            public float ComboWindowStart;
            public float ComboWindowEnd;
            public float ComboRate;
            public int ComboEndMode;
        }
        #endregion

        #region Type2
        public struct Type2
        {
            public int SubType;

            public EmitterEffect? Emitter; //0
            public SplinePathAnimation? SplineAnimation; //SplinePath
            public EmitterEffect? CollisionEmitter; //2 CollideEmitter
        }

        public struct EmitterEffect
        {
            public int ParticleCount;
            public int TrailCopies;
            public float EmissionWindow;
            public float TimeScale;
            public float SizeCenter;
            public float ParticleLifeCenter;
            public float SizeSpan;
            public float ParticleLifeSpan;
            public float TrailSpacing;
            public float[] Origin;
            public float[] SpawnAxisA;
            public float[] SpawnAxisB;
            public float[] VelocityBase;
            public float[] VelocityAxisA;
            public float[] VelocityAxisB;
            public float[] VelocityAxisC;
            public float[] Gravity;
            public float[] ColourStop1; //R,G,B,A
            public float[] ColourStop2;
            public float[] ColourStop3;
            public float[] ColourStop4;
            public int SpriteIndex;
            public int BlendMode;
        }

        public struct SplinePathAnimation
        {
            public int SplineIndex;
            public int EndMode;
            public int OrientationMode;
            public int InstanceCount;
            public float AnimationSpeed;
            public float YawOffset;
            public int ShowRouteLine;
            public float[] RouteLineColour; //R,G,B,A
        }
        #endregion


        public struct Type3
        {
            public int Command;
            public float Parameter;
        }

        public struct Type5
        {
            public int Mode;
            public int Compare;
            public float Threshold;
        }

        public struct InstanceEffect
        {
            public int InstanceIndex;
            public int EffectIndex;
        }

        public struct Type9
        {
            public int Command;
            public float Parameter;
        }

        public struct HUDTextEffect
        {          
            public float HudRed; 
            public float HudGreen;
            public float HudBlue;

            public string HudText;
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
