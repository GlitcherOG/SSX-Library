# SSX-Library
C#/.NET Library for Extracting, Creating, and Modifying files for SSX games. 

The library serves as a framework for GUI/CLI modding tools, but it can also be used as a standalone tool for non tool developers. The library comes with abstraction classes and json formats for simplifying game data parsing. For example, its own Model class that unifies character model types from multiple games into one. And a level extraction feature that extracts level data into readable json files, that includes terrain, props, triggers, textures, lighting and more.

The library was made to isolate the backend from the Windows only [SSX Collection Multitool](https://github.com/GlitcherOG/SSX-Collection-Multitool). Currently we're refactoring every part of the library to make it maintainable and cross-platform. We have a general checklist of the things we need to do, though many will be combined or removed. 
## Refactor Checklist

## Special Thanks
https://github.com/Erickson400/SSXTrickyModelExporter <br>
https://github.com/WouterBaeyens/Ssx3SshConverter <br>
https://github.com/SSXModding/bigfile <br>
https://github.com/SSXModding/ <br>
https://github.com/gibbed/Gibbed.RefPack <br>
