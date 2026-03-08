using System.Collections.Immutable;
using System.Text;
using SSX_Library.Internal.Utilities;

namespace SSX_Library;

/*
    Remarks:
    LOCL strings are null terminated. The amount of null terminated UTF16 characters
    at the end of a string is inconsistant accross games. Do to this we've decided
    to use 2 null UTF16 characters for all the string terminations. That includes
    only using 2 characters for the end of the LOCL section when saving back to disk.

    The Load function only reads one null character to detect if a string terminated.

    TextEntryCount is only updated when calling Save().

    More Info: https://ssx.computernewb.com/wiki/Formats/Common:LOC
*/

/// <summary>
/// Localization/Language files
/// </summary>
public sealed class LOC
{
    private readonly ImmutableArray<byte> _magicLOCH = [..Encoding.ASCII.GetBytes("LOCH")];
    private readonly ImmutableArray<byte> _magicLOCT = [..Encoding.ASCII.GetBytes("LOCT")];
    private readonly ImmutableArray<byte> _magicLOCL = [..Encoding.ASCII.GetBytes("LOCL")];
    private readonly List<TextEntry> _textEntries = [];
    private bool _usesLOCT;
    private LOCH _locH;
    private LOCT _locT;
    private LOCL _locL;

    /// <summary>
    /// Does the LOC have an LOCT section.
    /// </summary>
    // public bool UsesLOCT { get { return _usesLOCT; }}

    // /// <summary>
    // /// Get the number of text entries in the LOCL.
    // /// </summary>
    // public int GetTextCount() => (int)_locL.TextEntryCount;

    // /// <summary>
    // /// Get the string of a text entry
    // /// </summary>
    // public string GetText(int id) => _locL.TextEntries[id];

    // /// <summary>
    // /// Set the string of a text entry
    // /// </summary>
    // public void SetText(int id, string value) => _locL.TextEntries[id] = value;

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="filePath"> Path to the LOC file on disk.</param>
    public void Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);

        // Read LOCH
        if (!Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCH))
        {
            throw new InvalidDataException("Invalid/Corrupt LOC file. LOCH section not found.");
        }
        stream.Position += 4; // LochSize
        stream.Position += 8; // Unknown 8 bytes
        stream.Position += 4; // loclOffset

        // Read LOCT if available
        long loctPosition = stream.Position; // Invalid if no LOCT section exists.
        List<HashData> hashData = []; // Empty if no LOCT section exists.
        _usesLOCT = Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCT);
        stream.Position -= 4; // Restore from LOCT signature check
        if (_usesLOCT)
        {
            stream.Position += 4; // Signature
            stream.Position += 4; // HeaderSize
            stream.Position += 8; // Unknown 8 bytes
            while (true)
            {
                hashData.Add(new()
                {
                    Hash = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
                    ID = Reader.ReadUInt32(stream, ByteOrder.LittleEndian)
                });

                // Break if the next 4 bytes is the LOCL signature.
                bool endOfSection = Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCL);
                stream.Position -= 4; // Restore from LOCL signature check
                if (endOfSection) break;
            }
        }

        // Read LOCL
        long loclPosition = stream.Position;
        if (!Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCL))
        {
            throw new InvalidDataException("Invalid/Corrupt LOC file. LOCL section not found.");
        }
        stream.Position += 4; // Size
        stream.Position += 4; // Unknown
        uint textEntryCount = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
        List<uint> offsets = [];
        for (int _ = 0; _ < textEntryCount; _++)
        {
            offsets.Add(Reader.ReadUInt32(stream, ByteOrder.LittleEndian));
        }
        List<string> textList = [];
        foreach (uint offset in offsets)
        {
            stream.Position = loclPosition + offset;

            // Check if empty
            ushort firstCharacter = Reader.ReadUInt16(stream, ByteOrder.LittleEndian);
            stream.Position -= 2; // Restore
            if (firstCharacter == 0)
            {
                textList.Add("");
                continue;
            }

            // Parse string. Null terminations are excluded.
            string text = "";
            while (true)
            {
                // Check if null terminated
                uint nullSequence = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
                if (nullSequence == 0)
                {
                    textList.Add(text);
                    break;
                }
                stream.Position -= 4; // Restore

                text += Encoding.Unicode.GetString(Reader.ReadBytes(stream, 2));
            }
        }

        // Create text entries
        _textEntries.Clear();
        if (_usesLOCT)
        {
            for (int i = 0; i < hashData.Count; i++)
            {
                _textEntries.Add(new()
                {
                    Hash = hashData[i].Hash,
                    Text = textList[(int)hashData[i].ID],
                });
            }
        }
        else
        {
            for (int i = 0; i < textEntryCount; i++)
            {
                _textEntries.Add(new()
                {
                    Hash = 0,
                    Text = textList[i],
                });
            }
        }
    }

    /// <summary>
    /// Save an LOC file from memory to disk.
    /// </summary>
    /// <param name="filePath">Path to save the LOC. If empty it will save to the same place
    /// it was loaded from. </param>
    public void Save(string filePath)
    {
        using FileStream stream = File.Create(filePath);

        // Save LOCH
        Writer.WriteBytes(stream, [.._magicLOCH]);
        Writer.WriteUInt32(stream, _locH.LOCHSize, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _locH.Unk0, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _locH.Unk1, ByteOrder.LittleEndian);

        long LOCLOffsetPos = stream.Position;
        stream.Position += 4;

        // Save LOCT if used
        if (_usesLOCT)
        {
            Writer.WriteBytes(stream, [.._magicLOCT]);
            Writer.WriteUInt32(stream, 16, ByteOrder.LittleEndian);
            Writer.WriteFloat(stream, _locT.Unk0, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, _locT.Unk1, ByteOrder.LittleEndian);
            foreach (var hashTable in _locT.HashTable)
            {
                Writer.WriteUInt32(stream, hashTable.Hash, ByteOrder.LittleEndian);
                Writer.WriteUInt32(stream, hashTable.ID, ByteOrder.LittleEndian);
            }
        }

        // Save LOCL
        var locLPosition = (uint)stream.Position;

        //Write LOCL Offset
        stream.Position = LOCLOffsetPos;
        Writer.WriteUInt32(stream, locLPosition, ByteOrder.LittleEndian);
        stream.Position = locLPosition;
        Writer.WriteBytes(stream, [.._magicLOCL]);
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
        stream.Position = locLOffsetsPosition;
        foreach (var offset in realOffset)
        {
            Writer.WriteUInt32(stream, offset, ByteOrder.LittleEndian);
        }
        // Set LOCL size
        stream.Position = locLPosition + 4; // <- _locL.LOCLSize
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
        public uint LOCTHeaderSize; // Always 16
        public float Unk0;
        public uint Unk1;

        //Sorted from lowest to highest hash.
        // Size is LOCL.TextEntryCount.
        public List<HashData> HashTable; 
    }

    private struct HashData
    {
        public uint Hash;
        public uint ID; // The index of the text entry in LOCL
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
    private struct TextEntry
    {
        public uint Hash;
        public string Text;
    }
}