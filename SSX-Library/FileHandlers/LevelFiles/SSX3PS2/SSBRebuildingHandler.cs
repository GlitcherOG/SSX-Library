using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2;
using SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data;
using SSXLibrary.JsonFiles.SSX3;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2
{
    public class SSBRebuildingHandler
    {
        SSX3Config ssx3Config = new SSX3Config();
        List <WorldSSH> worldSSHs = new List<WorldSSH>();
        List<WorldSSH> lightmapSSH = new List<WorldSSH>();
        string ProjectPath = "";


        FileStream SSBStream;
        PSMHandler psmHandler = new PSMHandler();
        PHMHandler phmHandler = new PHMHandler();
        SDBHandler sdbHandler = new SDBHandler();



        public void PackSSB(string ConfigSSX, string SSBPath)
        {
            SSBStream = File.Create(SSBPath);

            //Verify Config Correct Version
            ssx3Config = SSX3Config.Load(ConfigSSX);
            ProjectPath = Path.GetDirectoryName(ConfigSSX);

            //Load Textures including Lightmaps
            //Convert Textures to the correct format
            worldSSHs = new List<WorldSSH>();
            string[] Textures = Directory.GetFiles(ProjectPath + "\\Textures", "*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < Textures.Length; i++)
            {
                WorldSSH worldSSH = new WorldSSH();
                worldSSH.MatrixFormat = 2;
                worldSSH.ImportImage(Textures[i]);
                worldSSHs.Add(worldSSH);
            }

            lightmapSSH = new List<WorldSSH>();
            string[] LigthmapTextures = Directory.GetFiles(ProjectPath + "\\Lightmaps", "*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < LigthmapTextures.Length; i++)
            {
                WorldSSH worldSSH = new WorldSSH();
                worldSSH.MatrixFormat = 2;
                worldSSH.ImportImage(Textures[i]);
                lightmapSSH.Add(worldSSH);
            }

            //For all Levels
            //Load All Json and convert to correct data types
            //Load Level Json
            LevelJsonHandler levelJsonHandler = LevelJsonHandler.Load(ProjectPath + "\\Levels.json");
            for (int i = 0; i < levelJsonHandler.LevelInfoList.Count; i++)
            {
                LoadLevelData(ProjectPath + "\\Levels\\" + levelJsonHandler.LevelInfoList[i] + "\\");
            }

            //

            SSBStream.Close();
        }

        /*        
        Rebuild Order

        Shape
        Shape Lightmaps
        Materials
        Lights
        Halo
        Models
        Instance
        Particle Model
        Particle Instance
        Patches
        Splines
        Collision
        Vis Curtains
        Sound Triggers
        World Painter
        Camera Trigger
        Audio Bank
        AIP
        Scripts
        NIS Table
        Missions
        Radar
        Avalanche Animation*/

        public void LoadLevelData(string LevelPath)
        {
            MaterialJsonHandler bin0JsonHandler = MaterialJsonHandler.Load(LevelPath + "Bin0.json");
            PatchesJsonHandler patchesJsonHandler = PatchesJsonHandler.Load(LevelPath + "Patches.json");
            MDRJsonHandler mDRJsonHandler = MDRJsonHandler.Load(LevelPath + "Prefabs.json");
            InstanceJsonHandler instanceJsonHandler = InstanceJsonHandler.Load(LevelPath + "Instances.json");
            //ParticleModel
            ParticleInstanceJsonHandler particleInstanceJsonHandler = ParticleInstanceJsonHandler.Load(LevelPath + "Bin5.json");
            Bin6JsonHandler bin6JsonHandler = Bin6JsonHandler.Load(LevelPath + "Bin6.json");
            //Halo
            SplineJsonHandler splineJsonHandler = SplineJsonHandler.Load(LevelPath + "Splines.json");
            VisCurtainJsonHandler visCurtainJsonHandler = VisCurtainJsonHandler.Load(LevelPath + "VisCurtain.json");
            //Collision
            //Sound Trigger
            //Scripts
            //CameraTrigger
            Bin18JsonHandler bin18JsonHandler = Bin18JsonHandler.Load(LevelPath + "Bin18.json");
            //Missions
            //Audio Bank
            //Radar
            //Avalanche Animation

            //Generate Raw Item Lists and psmhandler and phmhandler

            //Generate Speical Tree and Chunks

        }
    }
}
