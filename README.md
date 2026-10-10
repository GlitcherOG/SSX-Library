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
>   sampling, SSX On Tour MPF parsing, 8-bit palette limiting/ordering/alpha
>   in old SSH shapes, including compressed texture chunk alignment, and the
>   padded swizzled palettes of SSX 3 world shapes.
> - **Image processing dependencies:** ImageSharp.Drawing 3.1.2 brings in
>   ImageSharp 4.1.2, including the codec and metadata security fixes. The library
>   and its tests use the same dependency version.
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

## Special Thanks
https://github.com/Erickson400/SSXTrickyModelExporter <br>
https://github.com/WouterBaeyens/Ssx3SshConverter <br>
https://github.com/SSXModding/bigfile <br>
https://github.com/SSXModding/ <br>
https://github.com/gibbed/Gibbed.RefPack <br>
