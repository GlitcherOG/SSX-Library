using SSXLibrary.JsonFiles.SSX3;
using System;
using System.Collections.Generic;
using System.Text;

namespace SSX_Library.FileHandlers.LevelFiles.SSX3PS2
{
    public class SSBRebuildingHandler
    {


        public void PackSSB(string ConfigSSX)
        {
            //Verify Config Correct Version
            SSX3Config ssx3Config = SSX3Config.Load(ConfigSSX);
            string ProjectPath = Path.GetDirectoryName(ConfigSSX);

            //Load Level Json

            //Load Textures including Lightmaps
            //Convert Textures to the correct format

            for (int i = 0; i < length; i++)
            {
                
            }

            //For all Levels
            //Load All Json and convert to correct data types

            //


        }
    }
}
