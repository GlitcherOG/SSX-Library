using SSX_Library.Utilities;
using SSXLibrary.Utilities;

namespace SSX_Library;

/// <summary>
/// Language files
/// </summary>
public class LOC
{
    // TODO" Use Long instead of int for stream offsets and lengths
    // TODO: Rename privates with underscores.
    private string filePath = "";
    private byte[] headerBytes = [];
    private byte[] LOCLHeader = [];
    private int TextCount;
    private List<int> StringOffsets = [];
    
    public List<string> StringList = [];

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="path"> Path to the LOC file on disk.</param>
    /// <returns>True if loaded succesfully.</returns>
    public bool Load(string path)
    {
        if (!File.Exists(path)) return false;
        filePath = path;
        using Stream stream = File.OpenRead(path);

        //Find start of file
        long magicPos = ByteConv.FindBytePattern(stream, [0x4C, 0x4F, 0x43, 0x4C]);

        //Save Header of file
        stream.Position = 0;
        headerBytes = new byte[magicPos];
        stream.Read(headerBytes, 0, headerBytes.Length);

        //Save LOCL Header
        stream.Position = magicPos;
        LOCLHeader = new byte[12];
        stream.Read(LOCLHeader, 0, LOCLHeader.Length);

        //Save Ammount of Entires
        // TODO: Make this return a uint32 instead of int.
        TextCount = StreamUtil.ReadUInt32(stream);

        //Grab List of Offsets
        for (int i = 0; i < TextCount; i++)
        {
            int posLoc = StreamUtil.ReadUInt32(stream);
            StringOffsets.Add(posLoc);
        }

        //Using Offsets Grab Text
        for (int i = 0; i < TextCount; i++)
        {
            int Length = StringOffsets[i + 1] - StringOffsets[i];
            if (i + 1 >= TextCount)
                Length = (int)(stream.Length - stream.Position);
                
            byte[] byteString = new byte[Length];
            stream.Read(byteString, 0, Length);
            byte[] modString;

            //Check If Blank Entry
            // TODO: Whats with this big if.
            if (byteString.Length > 5)
            {
                modString = new byte[byteString.Length - 4];
            }
            else
            {
                modString = new byte[byteString.Length];
            }
            for (int a = 0; a < byteString.Length - 5; a++)
            {
                modString[a] = byteString[a];
            }
            string Text = System.Text.Encoding.Unicode.GetString(modString);
            StringList.Add(Text);
        }
        return true;
    }

    /// <summary>
    /// Save an LOC file from memory to disk.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>True if saved succesfully.</returns>
    public bool Save(string path = "")
    {
        if(path == "" || !File.Exists(path)) path = filePath;

        MemoryStream stream = new();
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(LOCLHeader, 0, LOCLHeader.Length);
        //TODO: Refactor WriteInt32
        StreamUtil.WriteInt32(stream, TextCount);
        //Write Intial Offset
        stream.Write(BitConverter.GetBytes(StringOffsets[0]), 0, 4);

        //Set New Offsets
        for (int i = 0; i < StringList.Count; i++)
        {
            string temp = StringList[i];
            MemoryStream stream1 = new();
            byte[] temp1 = System.Text.Encoding.Unicode.GetBytes(temp);
            stream1.Write(temp1, 0, temp1.Length);
            int Diff = (int)stream1.Length + StringOffsets[i] + 4;
            if (i < StringOffsets.Count - 1)
            {
                StringOffsets[i + 1] = Diff;
                stream.Write(BitConverter.GetBytes(Diff), 0, 4);
            }
        }

        //Set strings
        for (int i = 0; i < StringList.Count; i++)
        {
            byte[] temp;
            temp = System.Text.Encoding.Unicode.GetBytes(StringList[i]);
            stream.Write(temp, 0, temp.Length);
            for (int ai = 0; ai < 4; ai++)
            {
                stream.WriteByte(0x00);
            }
        }

        //Save File
        if (File.Exists(path)) File.Delete(path);
        var file = File.Create(path);
        stream.Position = 0;
        stream.CopyTo(file);
        file.Close();
        return true;
    }

    // LOCH
    // 0-3 Magic Words
    // 4-7 LOCT Offset (Or Size)
    // 8-11 Unknown (Flag? Always 0)
    // 12-15 Unknown 2 (Flag? Always 1)
    // 16-19 LOCL Offset

    // LOCT

    // LOCL
    // 0-3 Magic words
    // 4-7 File Size
    // 8-11 Unknown
    // 12-15 Ammount
    // 16-19 Offset Start
}