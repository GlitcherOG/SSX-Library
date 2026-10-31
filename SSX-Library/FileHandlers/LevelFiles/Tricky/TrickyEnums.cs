namespace SSXLibrary.FileHandlers.LevelFiles.Tricky
{
    //Known values for Tricky level file fields, ported from OpenSlope's research.
    //The fields that hold them stay int so the binary and JSON layouts are unchanged; cast to compare.

    #region SSF
    //SSFHandler.Effect.MainType
    public enum SSFMainType
    {
        Property = 0,               //type0, sub-typed by SSFPropertyType
        Emitter = 2,                //type2, sub-typed by SSFEmitterType
        AddDelta = 3,               //type3, a control command for the property node on the bound instance
        Wait = 4,                   //WaitTime
        ConditionalGate = 5,        //type5, continue or end the chain (SSFGateMode)
        ActOnInstance = 7,          //Instance
        PlaySound = 8,              //SoundPlay
        AddDelta9 = 9,              //type9, as AddDelta without its missing-node guard
        HudText = 12,               //hudTextEffect, OpenSlope addition: retail sends 12 to its do-nothing case
        ResetZone = 13,             //type13, puts the rider back on the race line
        ScoreMultiplier = 14,       //MultiplierScore
        TimeBonus = 16,             //Showoff only, no branch in SSFHandler (kept as UnknownPayload)
        SpeedBoost = 17,            //SpeedBoostDuration
        TrickBoost = 18,            //TrickBoostDuration
        CallFunction = 21,          //FunctionRunIndex
        CameraOperation = 23,       //no branch in SSFHandler (kept as UnknownPayload)
        Teleport = 24,              //TeleportInstanceIndex
        ToggleRail = 25,            //Spline
        CallFunctionDetached = 26,  //CallFunction with no owning rider, no branch in SSFHandler
    }

    //SSFHandler.Type0.SubType. Values marked * have no payload branch in SSFHandler
    public enum SSFPropertyType
    {
        Roller = 0,
        Debounce = 2,
        DeadNode = 5,
        Counter = 6,
        Boost = 7,
        Timer = 8,                  //*
        Rail = 9,                   //*
        UVScroll = 10,
        TextureFlip = 11,
        Fence = 12,
        Flag = 13,
        Cracked = 14,
        LapBoost = 15,
        RandomBoost = 16,           //*
        CrowdBox = 17,
        ZBoost = 18,
        UVScrollTextureFlip = 19,   //*
        MeshAnim = 20,
        TrickTrigger = 21,          //*
        Particle = 22,              //*
        Movie = 23,
        TubeEndBoost = 24,
        AnimObject = 256,
        AnimDelta = 257,
        AnimCombo = 258,
        AnimTextureFlip = 259,      //*
    }

    //SSFHandler.Type2.SubType
    public enum SSFEmitterType
    {
        Emitter = 0,
        SplineMover = 1,
        CollisionEmitter = 2,
    }

    //SSFHandler.Type0.DeadNodeMode
    public enum SSFDeadNodeMode
    {
        Destroy = 0,
        Pause = 1,
        Tombstone = 2,
        TombstoneFlagged = 3,
        BreakableKill = 4,          //hard kill/hide of the source, used by breakables
    }

    //SSFHandler.Type5.Mode
    public enum SSFGateMode
    {
        Speed = 0,                  //Threshold is a speed in cm/s
        Random = 1,                 //Threshold is the chance (0-1) of continuing
        HumanRider = 2,             //player only, not AI riders
        NoLiveNode = 3,             //only while the instance is not already running a node
    }

    //BoostEffect.Mode, TubeEndBoostEffect.Mode
    public enum SSFBoostMode
    {
        Continuous = 0,             //never retires, Window acts as a cooldown
        Timed = 1,                  //retires once Window expires with no rider inside. All retail placements
    }

    //UVScrolling.Mode. Any other value scrolls linearly
    public enum SSFUVScrollMode
    {
        Linear = 0,
        EasedPingPong = 1,
        ConstantSpeedPingPong = 2,
    }

    //AnimObjectEffect/AnimDeltaEffect/AnimComboEffect.LoopMode
    public enum SSFAnimLoopMode
    {
        PlayOnce = 0,
        Wrap = 1,
        PingPong = 2,
    }

    //AnimObjectEffect/AnimDeltaEffect/AnimComboEffect.DirectionMode
    public enum SSFAnimDirectionMode
    {
        Forward = 3,
        Reverse = 4,
    }

    //AnimComboEffect.ComboEndMode. Read for its sign: 0, positive or negative
    public enum SSFAnimComboEndMode
    {
        HoldComboPose = -1,         //stop, holding the last combo frame
        ResumeIdle = 0,             //back to the idle loop, can be triggered again
        StopAtIdlePose = 1,         //stop, back to the idle pose
    }

    //SplinePathAnimation.EndMode. Other values loop
    public enum SSFSplineEndMode
    {
        Finish = 0,
        Wrap = 1,
        PingPong = 2,
        HoldAtEnd = 3,
    }

    //SplinePathAnimation.OrientationMode. Other values follow yaw and pitch
    public enum SSFSplineOrientationMode
    {
        FollowYawAndPitch = 0,
        FollowYaw = 1,              //stays level
        FollowPitch = 2,            //fixed yaw
        Fixed = 3,
    }

    //SSFHandler.Spline.SplineStyle. The values are TrickySurfaceType surfaces (12 Wood, 13 metal)
    public enum SSFSplineStyle
    {
        MotionPath = -1,            //spline mover route (trains, gondolas), not grindable
        NonGrind = 1,               //rail an effect switches in later
        WoodRail = 12,
        MetalRail = 13,
    }

    //ObjectPropertiesStruct.CollsionMode
    public enum SSFCollisionMode
    {
        None = 0,
        TriangleProxy = 1,          //CollisonModelIndex
        BoundingBox = 2,
        PhysicsBodySpheres = 3,     //PhysicsIndex
    }

    //ObjectPropertiesStruct.BitFlags
    [Flags]
    public enum SSFObjectPropertyFlags
    {
        Visible = 1 << 0,
        PlayerCollision = 1 << 5,
        PlayerBounce = 1 << 7,
        UVScroll = 1 << 13,
    }

    //PhysicsData.OccupancyMaskEncoding
    public enum SSFOccupancyMaskEncoding
    {
        Raw = 0,
        RunLength = 1,              //signed control byte: negative copies -n literals, positive repeats the next byte n+1 times, 0 stops
    }
    #endregion

    #region PBD
    //Patch.SurfaceType, ObjectPropertiesStruct.SurfaceType
    public enum TrickySurfaceType
    {
        None = -1,                  //props: obstacle handling, not a surface
        Reset = 0,                  //out of bounds
        StandardSnow = 1,
        StandardOffTrack = 2,
        PowderedSnow = 3,
        SlowPowderedSnow = 4,
        Ice = 5,
        BounceUnskiable = 6,
        IceWaterNoTrail = 7,
        GlidySnow = 8,
        Rock = 9,
        Wall = 10,
        IceCrunchNoTrail = 11,
        Wood = 12,
        OffTrackMetal = 13,
        SpeedGrinding = 14,
        StandardUnknown = 15,
        Sand = 16,
        NoCollision = 17,
        ShowOffRampMetal = 18,
        ShowOffRampMetal2 = 19,     //metal sounds, the same audio group as 18
    }

    //Light.Type
    public enum TrickyLightType
    {
        Directional = 0,
        Spot = 1,
        Point = 2,
        Ambient = 3,
    }

    //TrickyMaterial.AppearanceFlags
    [Flags]
    public enum TrickyMaterialAppearanceFlags
    {
        DrawPriority = 1 << 17,     //opaque draw-order tiebreaker for coplanar decals
        AlphaBlend = 1 << 18,       //composites with the texture alpha
    }

    //Instance.LTGState, the LTG node list an instance is stored in
    public enum TrickyLTGState
    {
        NotListed = -1,
        Common = 0,                 //InstanceIndex
        Race = 1,                   //RaceInstanceIndex
        Gem = 2,                    //GemIndex, the Showoff-only object layer
    }
    #endregion

    #region ADL
    //ExternalSound.EmitterType
    public enum ADLEmitterType
    {
        Point = 0,
        OrientedEllipsoid = 1,
        DirectionalCone = 2,
        PointAlternateVoice = 3,
    }

    //ExternalSound falloff curve selector (type 0 U6, type 1 U11)
    public enum ADLFalloffCurve
    {
        OneMinusDistanceSquared = 0,    //1 - d^2
        RationalDistance = 1,
        Linear = 2,                     //1 - d
        RationalInverseDistance = 3,
        SquaredOneMinusDistance = 4,    //(1 - d)^2
        FlatThenLinear = 5,             //flat to 70%, then linear
    }
    #endregion

    #region AIP/SOP
    //PathEvent.EventType. 102 is checked for but has no known caller
    public enum PathEventType
    {
        Disabled = -1,
        FinishLine = 9,             //race line marker at EventStart == DistanceToFinish
        Checkpoint = 11,            //race line. In SOP, EventValue is the Showoff time bonus in seconds
        Lap = 12,                   //race line. Decrements the passes remaining
        Jump = 100,                 //AI path. EventValue: target speed km/h << 2 | two trick flags
        CruiseSpeed = 101,          //AI path. EventValue: target speed in km/h
        ResetBeforeRegion = 103,
        NoReset = 104,
        ResetAfterRegion = 105,
        ReselectPath = 300,         //AI re-evaluates nearby paths
    }
    #endregion
}
