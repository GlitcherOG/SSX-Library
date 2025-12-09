using System.Collections;
using System.Collections.Immutable;
using System.Text;
using SSX_Library.Utilities;

namespace SSX_Library;

/*
    Remarks:
    LOCL strings are null terminated. The amount of null terminated UTF16 characters
    at the end of a string is inconsistant accross games. Do to this we've decided
    to use 2 null UTF16 characters for all the string terminations. That includes
    only using 2 characters for the end of the LOCL section when saving back to disk.

    The Load function only reads one null character to detect if a string terminated.

    _locL.TextEntryCount is only updated when calling Save(). This is because the user can
    modify _locL.TextEntries anytime.
*/

/// <summary>
/// Localization/Language files
/// </summary>
public sealed class LOC
{
    private readonly ImmutableArray<byte> _locHMagicWord = [0x4C, 0x4F, 0x43, 0x48];
    private readonly ImmutableArray<byte> _locTMagicWord = [0x4C, 0x4F, 0x43, 0x54];
    private readonly ImmutableArray<byte> _locLMagicWord = [0x4C, 0x4F, 0x43, 0x4C];
    private bool _usesLOCT;
    private LOCH _locH;
    private LOCT _locT;
    private LOCL _locL;

    /// <summary>
    /// Add/Remove/Modify text entries from the LOC.
    /// </summary>
    public List<string> TextEntries
    {
        get { return _locL.TextEntries; }
        set { _locL.TextEntries = value; }
    }

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="path"> Path to the LOC file on disk.</param>
    public void Load(string path)
    {
        using FileStream stream = File.OpenRead(path);

        // Confirm LOCH signature was found
        var magicLOCH = Reader.ReadBytes(stream, 4);
        for (int i = 0; i < magicLOCH.Length; i++)
        {
            if (magicLOCH[i] != _locHMagicWord[i])
            {
                throw new InvalidDataException("Invalid/Corrupt LOC file. LOCH section not found.");
            }
        }

        // Create LOCH
        _locH = new()
        {
            MagicWord = [.. _locHMagicWord],
            LOCHSize = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Unk0 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Unk1 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            LOCLOffset = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
        };

        // Check if LOCT signature was found
        long LOCTPos = stream.Position;
        _usesLOCT = true;
        var magicLOCT = Reader.ReadBytes(stream, 4);
        for (int i = 0; i < magicLOCT.Length; i++)
        {
            if (magicLOCT[i] != _locTMagicWord[i])
            {
                _usesLOCT = false;
                break;
            }
        }

        stream.Position = _locH.LOCLOffset;
        var magicLOCL = Reader.ReadBytes(stream, 4);

        // Check if LOCL signature was found
        var locLPosition = _locH.LOCLOffset; // Used later on
        for (int i = 0; i < magicLOCL.Length; i++)
        {
            if (magicLOCL[i] != _locLMagicWord[i])
            {
                throw new InvalidDataException("Invalid/Corrupt LOC file. LOCL section not found.");
            }
        }

        // Create LOCL
        _locL = new()
        {
            MagicWord = [.._locLMagicWord],
            LOCLSize = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            Unk0 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            TextEntryCount = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
            TextEntryOffsets = [],
            TextEntries = [],
        };
        for (int i = 0; i < _locL.TextEntryCount; i++)
        {
            _locL.TextEntryOffsets.Add(Reader.ReadUInt32(stream, ByteOrder.LittleEndian));
        }
        foreach (var offset in _locL.TextEntryOffsets)
        {
            stream.Position = locLPosition + offset;
            string text = "";
            while (true)
            {
                var letterBytes = Reader.ReadBytes(stream, 2);

                // Check if null teminated
                if ((letterBytes[0] | letterBytes[1]) == 0)
                {
                    _locL.TextEntries.Add(text);
                    break;
                }
                string character = Encoding.Unicode.GetString(letterBytes);
                text += character;
            }
        }

        // Create LOCT
        if (_usesLOCT)
        {
            stream.Position = LOCTPos + 4;

            _locT = new()
            {
                MagicWord = [.. _locTMagicWord],
                LOCTHeaderSize = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
                Unk0 = Reader.ReadFloat(stream, ByteOrder.LittleEndian),
                Unk1 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),

                HashTable = new List<HashData>()
            };

            for (int i = 0; i < _locL.TextEntryCount; i++)
            {
                var NewHashData = new HashData()
                {
                    Hash = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
                    ID = Reader.ReadUInt32(stream, ByteOrder.LittleEndian)
                };

                _locT.HashTable.Add(NewHashData);
            }
        }
    }

    /// <summary>
    /// Save an LOC file from memory to disk.
    /// </summary>
    /// <param name="path">Path to save the LOC. If empty it will save to the same place
    /// it was loaded from. </param>
    public void Save(string path)
    {
        using FileStream stream = File.Create(path);

        // Save LOCH
        Writer.WriteBytes(stream, [.._locHMagicWord]);
        Writer.WriteUInt32(stream, _locH.LOCHSize, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _locH.Unk0, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _locH.Unk1, ByteOrder.LittleEndian);

        long LOCLOffsetPos = stream.Position;
        stream.Position += 4;

        // Save LOCT if used
        if (_usesLOCT)
        {
            Writer.WriteBytes(stream, [.._locTMagicWord]);
            Writer.WriteUInt32(stream, 16, ByteOrder.LittleEndian);
            Writer.WriteFloat(stream, _locT.Unk0, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, _locT.Unk1, ByteOrder.LittleEndian);

            for (int i = 0; i < _locT.HashTable.Count; i++)
            {
                Writer.WriteUInt32(stream, _locT.HashTable[i].Hash, ByteOrder.LittleEndian);
                Writer.WriteUInt32(stream, _locT.HashTable[i].ID, ByteOrder.LittleEndian);
            }
        }

        // Save LOCL
        var locLPosition = (uint)stream.Position;

        //Write LOCL Offset
        stream.Position = LOCLOffsetPos;
        Writer.WriteUInt32(stream, locLPosition, ByteOrder.LittleEndian);

        stream.Position = locLPosition;

        Writer.WriteBytes(stream, [.._locLMagicWord]);
        Writer.WriteUInt32(stream, 0, ByteOrder.LittleEndian); // Placeholder
        Writer.WriteUInt32(stream, _locL.Unk0, ByteOrder.LittleEndian);
        _locL.TextEntryCount = (uint)_locL.TextEntries.Count; // Sync it
        Writer.WriteUInt32(stream, _locL.TextEntryCount, ByteOrder.LittleEndian);
        var locLOffsetsPosition = (uint)stream.Position; // Used later
        Writer.WriteBytes(stream, new byte[_locL.TextEntryCount * sizeof(uint)]); // Placeholder for offsets
        List<uint> realOffset = [];
        foreach (var textEntry in _locL.TextEntries)
        {
            realOffset.Add((uint)stream.Position);
            Writer.WriteBytes(stream, Encoding.Unicode.GetBytes(textEntry));
            Writer.WriteBytes(stream, [0, 0, 0, 0]); // Null termination with two UTF16 characters
        }
        
        // Rewrite the offsets to real ones
        // uint realLOCLSize = locLPosition - (uint)stream.Position;
        stream.Position = locLOffsetsPosition;
        for (int i = 0; i < _locL.TextEntryCount; i++)
        {
            Writer.WriteUInt32(stream, realOffset[i], ByteOrder.LittleEndian);
        }
        // Set LOCL size
        stream.Position = locLPosition + 4; // _locL.LOCLSize
        Writer.WriteUInt32(stream, (uint)stream.Length - locLPosition + _locH.LOCHSize, ByteOrder.LittleEndian);
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
        public uint LOCTHeaderSize;
        public float Unk0; //Might be uint instead but appears to be a float
        public uint Unk1;

        public List<HashData> HashTable; //Sorted from lowest to highest hash
    }

    private struct HashData
    {
        public uint Hash;
        public uint ID; //Links back to Text Index
    }

    private struct LOCL
    {
        public byte[] MagicWord;
        public uint LOCLSize;
        public uint Unk0;
        public uint TextEntryCount;
        public List<uint> TextEntryOffsets; // Length: TextEntryCount. Relative to LOCL.
        public List<string> TextEntries; // Length: TextEntryCount
    }
}