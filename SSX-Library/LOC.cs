using System.Collections.Immutable;
using System.Buffers.Binary;

namespace SSX_Library;

/*
    Remarks:
    LOCL strings are null terminated. The amount of null terminated UTF16 characters
    at the end of a string is inconsistant accross games. Do to this we've decided
    to use 2 null UTF16 characters for all the string terminations. That includes
    only using 2 characters for the end of the LOCL section when saving back to disk.

    The Load function only reads one null character to detect if a string terminated.
*/

/// <summary>
/// Localization/Language files
/// </summary>
public sealed class LOC
{
    private readonly ImmutableArray<byte> _locHMagicWord = [0x4C, 0x4F, 0x43, 0x48];
    private readonly ImmutableArray<byte> _locTMagicWord = [0x4C, 0x4F, 0x43, 0x54];
    private readonly ImmutableArray<byte> _locLMagicWord = [0x4C, 0x4F, 0x43, 0x4C];
    private string _filePath = "";
    private bool _usesLOCT;
    private LOCH _locH;
    private LOCT _locT;
    private LOCL _locL;

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="path"> Path to the LOC file on disk.</param>
    public void Load(string path)
    {
        using Stream stream = File.OpenRead(path);
        _filePath = path;

        // Confirm LOCH signature was found
        var buf4 = new byte[4];
        stream.Read(buf4);
        for (int i = 0; i < buf4.Length; i++)
        {
            if (buf4[i] != _locHMagicWord[i])
            {
                throw new InvalidDataException("Invalid/Corrupt LOC file. LOCH section not found.");
            }
        }

        // Create LOCH
        _locH = new() {MagicWord = [.._locHMagicWord]};
        stream.Read(buf4);
        _locH.LOCHSize = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        stream.Read(buf4);
        _locH.Unk0 = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        stream.Read(buf4);
        _locH.Unk1 = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        stream.Read(buf4);
        _locH.LOCLOffset = BinaryPrimitives.ReadUInt32LittleEndian(buf4);

        // Check if LOCT signature was found
        _usesLOCT = true;
        stream.Read(buf4);
        for (int i = 0; i < buf4.Length; i++)
        {
            if (buf4[i] != _locTMagicWord[i])
            {
                _usesLOCT = false;
                break;
            }
        }

        // Create LOCT
        if (_usesLOCT)
        {
            _locT = new() {MagicWord = [.._locTMagicWord]};
            uint loctSize = _locH.LOCLOffset - (uint)stream.Position;
            _locT.Data = new byte[loctSize];
            stream.Read(_locT.Data);
        }
        else
        {
            stream.Position -= 4; // Go back to re-read the signature for LOCL
        }

        // Check if LOCL signature was found
        var locLPosition = (uint)stream.Position;
        stream.Read(buf4);
        for (int i = 0; i < buf4.Length; i++)
        {
            if (buf4[i] != _locLMagicWord[i])
            {
                throw new InvalidDataException("Invalid/Corrupt LOC file. LOCL section not found.");
            }
        }

        // Create LOCL
        _locL = new() {MagicWord = [.._locLMagicWord]};
        stream.Read(buf4);
        _locL.LOCLSize = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        stream.Read(buf4);
        _locL.Unk0 = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        stream.Read(buf4);
        _locL.TextEntryCount = BinaryPrimitives.ReadUInt32LittleEndian(buf4);
        _locL.TextEntryOffsets = [];
        for (int i = 0; i < _locL.TextEntryCount; i++)
        {
            stream.Read(buf4);
            _locL.TextEntryOffsets.Add(BinaryPrimitives.ReadUInt32LittleEndian(buf4));
        }
        _locL.TextEntries = [];
        foreach (var offset in _locL.TextEntryOffsets)
        {
            stream.Position = locLPosition + offset;
            string text = "";
            while (true)
            {
                var buf2 = new byte[2];
                stream.Read(buf2);

                // Check if null teminated
                if ((buf2[0] | buf2[1]) == 0)
                {
                    _locL.TextEntries.Add(text);
                    break;
                }
                string character = System.Text.Encoding.Unicode.GetString(buf2);
                text += character;
            }
        }
    }

    /// <summary>
    /// Save an LOC file from memory to disk.
    /// </summary>
    /// <param name="path"></param>
    public void Save(string path = "")
    {
        // if(path == "" || !File.Exists(path)) path = filePath;

        // MemoryStream stream = new();
        // stream.Write(headerBytes, 0, headerBytes.Length);
        // stream.Write(LOCLHeader, 0, LOCLHeader.Length);
        // //TODO: Refactor WriteInt32
        // StreamUtil.WriteInt32(stream, TextCount);
        // //Write Intial Offset
        // stream.Write(BitConverter.GetBytes(StringOffsets[0]), 0, 4);

        // //Set New Offsets
        // for (int i = 0; i < StringList.Count; i++)
        // {
        //     string temp = StringList[i];
        //     MemoryStream stream1 = new();
        //     byte[] temp1 = System.Text.Encoding.Unicode.GetBytes(temp);
        //     stream1.Write(temp1, 0, temp1.Length);
        //     int Diff = (int)stream1.Length + StringOffsets[i] + 4;
        //     if (i < StringOffsets.Count - 1)
        //     {
        //         StringOffsets[i + 1] = Diff;
        //         stream.Write(BitConverter.GetBytes(Diff), 0, 4);
        //     }
        // }

        // //Set strings
        // for (int i = 0; i < StringList.Count; i++)
        // {
        //     byte[] temp;
        //     temp = System.Text.Encoding.Unicode.GetBytes(StringList[i]);
        //     stream.Write(temp, 0, temp.Length);
        //     for (int ai = 0; ai < 4; ai++)
        //     {
        //         stream.WriteByte(0x00);
        //     }
        // }

        // //Save File
        // if (File.Exists(path)) File.Delete(path);
        // var file = File.Create(path);
        // stream.Position = 0;
        // stream.CopyTo(file);
        // file.Close();
        // return true;
    }

    private struct LOCH
    {
        public byte[] MagicWord;
        public uint LOCHSize;
        public uint Unk0;
        public uint Unk1;
        public uint LOCLOffset; // Relative to file.
    }

    private struct LOCT
    {
        public byte[] MagicWord;
        public byte[] Data;
    }

    private struct LOCL
    {
        public byte[] MagicWord;
        public uint LOCLSize;
        public uint Unk0;
        public uint TextEntryCount;
        public List<uint> TextEntryOffsets; // Length: TextEntryCount. Relative to LOCL.
        public List<string> TextEntries; 
    }
}