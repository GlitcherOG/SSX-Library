# SSX-Library
C#/.NET Library for Extracting, Creating, and Modifying files for SSX games. 

> ## This is a modified fork
>
> This repository is a fork of [GlitcherOG/SSX-Library](https://github.com/GlitcherOG/SSX-Library),
> **modified by swax** and maintained as the build input for
> another tool. It is not the upstream project.
> Modifications are Copyright (C) 2026 swax and, like the upstream work, are
> licensed **GPL-3.0** — see [LICENSE.txt](LICENSE.txt).
>
> ### Changes from upstream (2025–2026)
>
> - **Audio decoding, new here:** EA-XA and PS-ADPCM codecs, BNK sound-bank
>   support, and an EA MicroTalk (UTK) speech decoder. `UtkCodec.cs` is a port of
>   vgmstream's `utkdec.c` and carries vgmstream's ISC copyright and permission
>   notice in its own file header.
> - **Lightmaps:** terrain lightmaps decoded from the alpha channel
>   (`ResolveLightmapImage` / `ExtractLightmapIntensity`), with per-image
>   brightness guards for mixed-brightness banks.
> - **Tricky level formats:** MAP section parsing by marker, PBD patch resources,
>   particle model indexing, SSF emitter and OBJ-import fixes, `.ltg` cell
>   listing corrected to match the original grid rules, and `.ssh` reuse via
>   seeded texture slots on repack.
> - **Fixes:** AIP/SOP writer path vectors and bounding boxes, Bézier arc-length
>   sampling, SSX On Tour MPF parsing, and 8-bit palette limiting/ordering/alpha
>   in old SSH shapes.
> - **Vendored SharpTriStrip:** `SSX-Library/SharpTriStrip/` is MaxHwoy's C# port
>   of NVidia's NvTriStrip algorithm and remains under its BSD-3-Clause license,
>   not this fork's general GPL terms. The complete required notice is retained
>   in [`SSX-Library/SharpTriStrip/LICENSE.txt`](SSX-Library/SharpTriStrip/LICENSE.txt)
>   and copied beside binary build output as `licenses/SharpTriStrip-LICENSE.txt`.
>
> `git log --oneline upstream/main..HEAD` gives the authoritative list. Fixes are
> offered back upstream where they are generally applicable; please report
> library bugs to upstream first unless they are specific to something above.

The library serves as a framework for GUI/CLI modding tools, but it can also be used as a standalone tool for non tool developers. The library comes with abstraction classes and json formats for simplifying game data parsing. For example, its own Model class that unifies character model types from multiple games into one. And a level extraction feature that extracts level data into readable json files, that includes terrain, props, triggers, textures, lighting and more.

The library was made to isolate the backend from the Windows only [SSX Collection Multitool](https://github.com/GlitcherOG/SSX-Collection-Multitool). Currently we're refactoring every part of the library to make it maintainable and cross-platform. We have a general checklist of the things we need to do, though many will be combined or removed. 
## Refactor Checklist
- [ ] DATAudio.cs
- [ ] HDRHandler.cs

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
- [ ] LUIHandler.cs
- [ ] MusicINFHandler.cs

FileHandlers\Textures:
- [ ] GTFHandler.cs
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
- [ ] ImageUtil.cs
- [ ] StreamUtil.cs

## Special Thanks
https://github.com/Erickson400/SSXTrickyModelExporter <br>
https://github.com/WouterBaeyens/Ssx3SshConverter <br>
https://github.com/SSXModding/bigfile <br>
https://github.com/SSXModding/ <br>
https://github.com/gibbed/Gibbed.RefPack <br>
