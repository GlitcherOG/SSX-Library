# SSX-Library
C#/.NET Library for Extracting, Creating, and Modifying files for SSX games. 

The library serves as a framework for GUI/CLI modding tools, but it can also be used as a standalone tool for non tool developers. The library comes with abstraction classes and json formats for simplifying game data parsing. For example, its own Model class that unifies character model types from multiple games into one. And a level extraction feature that extracts level data into readable json files, that includes terrain, props, triggers, textures, lighting and more.

The library was made to isolate the backend from the Windows only [SSX Collection Multitool](https://github.com/GlitcherOG/SSX-Collection-Multitool). Currently we're refactoring every part of the library to make it maintainable and cross-platform. We have a general checklist of the things we need to do, though many will be combined or removed. 
## Refactor Checklist
- [x] BigHandler.cs
- [ ] DATAudio.cs
- [ ] HDRHandler.cs
- [x] LOCHandler.cs
- [x] NewBigHandler.cs
- [ ] RefpackHandler.cs

FileHandlers\Audio:
- [ ] EAAudioHandler.cs

FileHandlers\LevelFiles\OGPS2:
- [ ] AIPHandler.cs
- [ ] MapHandler.cs
- [ ] WDFHandler.cs
- [ ] WDRHandler.cs
- [ ] WDSHandler.cs
- [ ] WDXHandler.cs
- [ ] WFXHandler.cs

FileHandlers\LevelFiles\OnTourPSP:
- [ ] xsmFileHandler.cs

FileHandlers\LevelFiles\SSX3PS2:
- [ ] PHMHandler.cs
- [ ] PSMHandler.cs
- [ ] SDBHandler.cs
- [ ] SSBHandler.cs

FileHandlers\LevelFiles\SSX3PS2\SSBData:
- [ ] WorldAIP.cs
- [ ] WorldBin0.cs
- [ ] WorldBin12.cs
- [ ] WorldBin18.cs
- [ ] WorldBin3.cs
- [ ] WorldBin5.cs
- [ ] WorldBin6.cs
- [ ] WorldCameraTriggers.cs
- [ ] WorldCommon.cs
- [ ] WorldMDR.cs
- [ ] WorldOldSSH.cs
- [ ] WorldPatch.cs
- [ ] WorldSpline.cs
- [ ] WorldSSH.cs
- [ ] WorldVisCurtain.cs

FileHandlers\LevelFiles\TrickyPS2:
- [ ] ADLHandler.cs
- [ ] AIPSOPHandler.cs
- [ ] LTGHandler.cs
- [ ] MapHandler.cs
- [ ] objSSFHandler.cs
- [ ] objTriPBDHandler.cs
- [ ] PBDHandler.cs
- [ ] SSFHandler.cs

FileHandlers\Models:
- [ ] aflHandler.cs
- [ ] glftHandler.cs
- [ ] OpenBXMFHandler.cs

FileHandlers\Models\SSX2012:
- [ ] CRSFHandler.cs
- [ ] GEOMHandler.cs

FileHandlers\Models\SSX3:
- [ ] SSX3GCMNF.cs
- [ ] SSX3GCModelCombiner.cs
- [ ] SSX3PS2ModelCombiner.cs
- [ ] SSX3PS2MPF.cs

FileHandlers\Models\SSXBlur:
- [ ] SSXBlurGCMNF.cs
- [ ] SSXBlurModelCombiner.cs

FileHandlers\Models\SSXOG:
- [ ] adfHandler.cs
- [ ] SSXMPFModelHandler.cs
- [ ] SSXOGModelCombiner.cs

FileHandlers\Models\SSXOnTour:
- [ ] SSXOnTourMPF.cs
- [ ] SSXOnTourPS2ModelCombiner.cs

FileHandlers\Models\SSXTricky:
- [ ] TrickyGCMNF.cs
- [ ] TrickyGCModelCombiner.cs
- [ ] TrickyPS2ModelCombiner.cs
- [ ] TrickyPS2MPF.cs
- [ ] TrickyXboxModelCombiner.cs
- [ ] TrickyXboxMXF.cs

FileHandlers\SSX2012:
- [ ] InfoDLCHandler.cs
- [ ] VaultBinHandler.cs
- [ ] VaultHandler.cs

FileHandlers\SSX3:
- [ ] BoltPS2Handler.cs
- [ ] CHARDBLHandler.cs
- [ ] LUIHandler.cs
- [ ] MusicINFHandler.cs

FileHandlers\Textures:
- [ ] GTFHandler.cs
- [x] NewSSHHandler.cs
- [x] OldSSHHandler.cs
- [x] OldXSHHandler.cs
- [ ] SMPHandler.cs

JsonFiles:
- [ ] SSXOGLevelInterface.cs
- [ ] TrickyLevelInterface.cs

JsonFiles\SSX3:
- [ ] AIPJsonHandler.cs
- [ ] Bin0JsonHandler.cs
- [ ] Bin18JsonHandler.cs
- [ ] Bin3JsonHandler.cs
- [ ] Bin5JsonHandler.cs
- [ ] Bin6JsonHandler.cs
- [ ] LevelJsonHandler.cs
- [ ] MDRJsonHandler.cs
- [ ] PatchesJsonHandler.cs
- [ ] SplineJsonHandler.cs
- [ ] SSX3Config.cs
- [ ] VisCurtainJsonHandler.cs

JsonFiles\SSXOG:
- [ ] AIPJsonHandler.cs
- [ ] InstanceJsonHandler.cs
- [ ] MaterialsJsonHandler.cs
- [ ] PatchesJsonHandler.cs
- [ ] PrefabJsonHandler.cs
- [ ] SplinesJsonHandler.cs
- [ ] SSXOGConfig.cs
- [ ] WFXJsonHandler.cs

JsonFiles\Tricky:
- [ ] AIPSOPJsonHandler.cs
- [ ] CameraJSONHandler.cs
- [ ] InstanceJsonHandler.cs
- [ ] LightJsonHandler.cs
- [ ] MaterialJsonHandler.cs
- [ ] ModelJsonHandler.cs
- [ ] ParticleInstanceJsonHandler.cs
- [ ] ParticleModelJsonHandler.cs
- [ ] PatchesJsonHandler.cs
- [ ] SplineJsonHandler.cs
- [ ] SSFJsonHandler.cs
- [ ] SSXTrickyConfig.cs

Utilities:
- [ ] BezierUtil.cs
- [x] ByteUtil.cs
- [x] MathUtil.cs
- [x] ConsoleWindow.cs
- [x] ErrorManager.cs
- [x] JsonUtil.cs
- [ ] ImageUtil.cs
- [ ] StreamUtil.cs

## Special Thanks
https://github.com/Erickson400/SSXTrickyModelExporter <br>
https://github.com/WouterBaeyens/Ssx3SshConverter <br>
https://github.com/SSXModding/bigfile <br>
https://github.com/SSXModding/ <br>
https://github.com/gibbed/Gibbed.RefPack <br>
