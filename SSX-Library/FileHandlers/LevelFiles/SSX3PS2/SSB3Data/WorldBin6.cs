using SSXLibrary.JsonFiles.SSX3;
using SSXLibrary.Internal.Utilities;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSB3Data
{
    //Lights
    //What decodes cleanly from level C (11 lights), the ints from U12 to U25 are floats stored as raw bits:
    // - U4 Type, 0 and 3 have no position (global, one of each in every level and its sky), 1 Spot, 2 Point
    // - U7 Range, the bounding box is the position +/- U7
    // - U6, U9, U10 look like the colour (Point lights come out blue 0.56 0.62 1, Spot lights warm 0.8 0.77 0.43, global 1 1 1)
    // - U11, U12, U13 Spot direction (unit vector)
    // - U14, U15, U16 Position, U17 to U19 and U20 to U22 the bounding box
    // - U23, U24 Spot inner and outer cone as cosines (0.94 / 0.866 is 20 / 30 degrees)
    public class WorldBin6
    {
        public int U0;
        public int U1;
        public int U2;
        public int U3;

        public int U4; //Type
        public float U5;
        public float U6;
        public float U7; //Range

        public float U8;
        public float U9;
        public float U10;
        public float U11; //Spot Direction X

        public int U12; //Spot Direction Y (float bits)
        public int U13; //Spot Direction Z (float bits)
        public int U14; //Position X (float bits)
        public int U15; //Position Y (float bits)

        public int U16; //Position Z (float bits)
        public int U17; //Bounding box (float bits)
        public int U18;
        public int U19;

        public int U20; //Bounding box (float bits)
        public int U21;
        public int U22;
        public int U23; //Spot inner cone cosine (float bits)

        public int U24; //Spot outer cone cosine (float bits)
        public int U25;
        public int U26;
        public int U27;


        public void LoadData(Stream stream)
        {
            U0 = StreamUtil.ReadInt32(stream);
            U1 = StreamUtil.ReadInt32(stream);
            U2 = StreamUtil.ReadInt32(stream);
            U3 = StreamUtil.ReadInt32(stream);

            U4 = StreamUtil.ReadInt32(stream);
            U5 = StreamUtil.ReadFloat(stream);
            U6 = StreamUtil.ReadFloat(stream);
            U7 = StreamUtil.ReadFloat(stream);

            U8 = StreamUtil.ReadFloat(stream);
            U9 = StreamUtil.ReadFloat(stream);
            U10 = StreamUtil.ReadFloat(stream);
            U11 = StreamUtil.ReadFloat(stream);

            U12 = StreamUtil.ReadInt32(stream);
            U13 = StreamUtil.ReadInt32(stream);
            U14 = StreamUtil.ReadInt32(stream);
            U15 = StreamUtil.ReadInt32(stream);

            U16 = StreamUtil.ReadInt32(stream);
            U17 = StreamUtil.ReadInt32(stream);
            U18 = StreamUtil.ReadInt32(stream);
            U19 = StreamUtil.ReadInt32(stream);

            U20 = StreamUtil.ReadInt32(stream);
            U21 = StreamUtil.ReadInt32(stream);
            U22 = StreamUtil.ReadInt32(stream);
            U23 = StreamUtil.ReadInt32(stream);

            U24 = StreamUtil.ReadInt32(stream);
            U25 = StreamUtil.ReadInt32(stream);
            U26 = StreamUtil.ReadInt32(stream);
            U27 = StreamUtil.ReadInt32(stream);
        }

        public Bin6JsonHandler.Bin6File ToJSON()
        {
            Bin6JsonHandler.Bin6File bin3File = new Bin6JsonHandler.Bin6File();

            bin3File.U0 = U0;
            bin3File.U1 = U1;
            bin3File.U2 = U2;
            bin3File.U3 = U3;

            bin3File.U4 = U4;
            bin3File.U5 = U5;
            bin3File.U6 = U6;
            bin3File.U7 = U7;

            bin3File.U8 = U8;
            bin3File.U9 = U9;
            bin3File.U10 = U10;
            bin3File.U11 = U11;

            bin3File.U12 = U12;
            bin3File.U13 = U13;
            bin3File.U14 = U14;
            bin3File.U15 = U15;

            bin3File.U16 = U16;
            bin3File.U17 = U17;
            bin3File.U18 = U18;
            bin3File.U19 = U19;

            bin3File.U20 = U20;
            bin3File.U21 = U21;
            bin3File.U22 = U22;
            bin3File.U23 = U23;

            bin3File.U24 = U24;
            bin3File.U25 = U25;
            bin3File.U26 = U26;
            bin3File.U27 = U27;

            return bin3File;
        }
    }
}
