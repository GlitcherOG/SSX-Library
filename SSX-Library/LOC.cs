using System.Collections.Immutable;
using System.Text;
using SSX_Library.Internal.Utilities;

namespace SSX_Library;

/*
    TODO: Update this remark. Also explain that saving doesnt take into account duplicated.
    It duplicates text if its used in multiple hashes, its unconfirmed if there are any hashData
    with the same offsets. This makes it easier to write since I can write the hashes in the same
    order as the offsets. I doubt the hashData havign different IDs from the original is going to be
    a problem.

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
    private readonly List<TextEntry> _textEntries = []; // Always sorted by hash.
    private UnknwonValues _unknownValues;
    private bool _usesLOCT;

    /// <summary>
    /// Does the LOC have an LOCT section?
    /// </summary>
    public bool UsesLOCT { get { return _usesLOCT; }}

    /// <summary>
    /// The number of text entries.
    /// </summary>
    public int TextCount { get { return _textEntries.Count; }}

    /// <summary>
    /// Get and find a text entry's string by it's hash.
    /// </summary>
    public string GetTextByHash(uint hash)
    {
        if (!_usesLOCT)
        {
            throw new KeyNotFoundException("The LOCT section does not exist for this LOC. Accessing the hash table is not possible.");
        }
        foreach (var entry in _textEntries)
        {
            if (entry.Hash == hash)
            {
                return entry.Text;
            }
        }
        throw new KeyNotFoundException($"Text entry with hash {hash} was not found.");
    }

    /// <summary>
    /// Get and find a text entry's string by it's id.
    /// </summary>
    public string GetTextByID(int id)
    {
        if (id >= _textEntries.Count)
        {
            throw new KeyNotFoundException($"Text entry with id {id} was not found.");
        }
        return _textEntries[id].Text;
    }

    /// <summary>
    /// Set a text entry's string by it's hash.
    /// </summary>
    public void SetTextByHash(uint hash, string text)
    {
        if (!_usesLOCT)
        {
            throw new KeyNotFoundException("The LOCT section does not exist for this LOC. Accessing the hash table is not possible.");
        }
        for (int i = 0; i < _textEntries.Count; i++)
        {
            if (_textEntries[i].Hash == hash)
            {
                TextEntry entry = _textEntries[i];
                entry.Text = text;
                _textEntries[i] = entry;
                return;
            }
        }
        throw new KeyNotFoundException($"Text entry with hash {hash} was not found.");
    }

    /// <summary>
    /// Set a text entry's string by it's id.
    /// </summary>
    public void SetTextByID(int id, string text)
    {
        if (id >= _textEntries.Count)
        {
            throw new KeyNotFoundException($"Text entry with id {id} was not found.");
        }
        TextEntry entry = _textEntries[id];
        entry.Text = text;
        _textEntries[id] = entry;
        return; 
    }

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="filePath"> Path to the LOC file.</param>
    public void Load(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        _unknownValues = new();

        // Read LOCH
        if (!Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCH))
        {
            throw new InvalidDataException("Invalid/Corrupt LOC file. LOCH section not found.");
        }
        stream.Position += 4; // LochSize
        _unknownValues.LOCH0 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
        _unknownValues.LOCH1 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
        stream.Position += 4; // loclOffset

        // Read LOCT if used
        long loctPosition = stream.Position; // Invalid if no LOCT section exists.
        List<HashData> hashData = []; // Empty if no LOCT section exists.
        _usesLOCT = Reader.ReadBytes(stream, 4).SequenceEqual(_magicLOCT);
        stream.Position -= 4; // Restore from LOCT signature check
        if (_usesLOCT)
        {
            stream.Position += 4; // Signature
            stream.Position += 4; // HeaderSize
            _unknownValues.LOCT0 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
            _unknownValues.LOCT1 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
            while (true)
            {
                hashData.Add(new()
                {
                    Hash = Reader.ReadUInt32(stream, ByteOrder.LittleEndian),
                    Id = Reader.ReadUInt32(stream, ByteOrder.LittleEndian)
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
        _unknownValues.LOCL0 = Reader.ReadUInt32(stream, ByteOrder.LittleEndian);
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
                    Text = textList[(int)hashData[i].Id],
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
    /// <param name="filePath">Path to save the LOC.</param>
    public void Save(string filePath)
    {
        using FileStream stream = File.Create(filePath);

        // Write LOCH
        Writer.WriteBytes(stream, [.._magicLOCH]);
        Writer.WriteUInt32(stream, 20, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _unknownValues.LOCH0, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, _unknownValues.LOCH1, ByteOrder.LittleEndian);
        long loclOffsetPosition = stream.Position; // Set later
        stream.Position += 4;

        // Write LOCT if used
        if (_usesLOCT)
        {
            Writer.WriteBytes(stream, [.._magicLOCT]);
            Writer.WriteUInt32(stream, 16, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, _unknownValues.LOCT0, ByteOrder.LittleEndian);
            Writer.WriteUInt32(stream, _unknownValues.LOCT1, ByteOrder.LittleEndian);
            for (int i = 0; i < _textEntries.Count; i++)
            {
                Writer.WriteUInt32(stream, _textEntries[i].Hash, ByteOrder.LittleEndian);
                Writer.WriteUInt32(stream, (uint)i, ByteOrder.LittleEndian);
            }
        }

        // Update LOCL offset on LOCH
        long loclPosition = stream.Position;
        stream.Position = loclOffsetPosition;
        Writer.WriteUInt32(stream, (uint)loclPosition, ByteOrder.LittleEndian);
        stream.Position = loclPosition;

        // Write LOCL
        Writer.WriteBytes(stream, [.._magicLOCL]);
        long loclSizePosition = stream.Position; // Set later 
        stream.Position += 4;
        Writer.WriteUInt32(stream, _unknownValues.LOCL0, ByteOrder.LittleEndian);
        Writer.WriteUInt32(stream, (uint)_textEntries.Count, ByteOrder.LittleEndian);
        long offsetListPosition = stream.Position; // Used later
        stream.Position += _textEntries.Count * sizeof(uint); // Placeholder for offsets

        // Write LOCL Text
        List<long> realOffset = [];
        foreach (var entry in _textEntries)
        {
            realOffset.Add(stream.Position);
            if (entry.Text == "")
            {
                Writer.WriteBytes(stream, [0, 0]); // Null character
            }
            else
            {
                Writer.WriteBytes(stream, Encoding.Unicode.GetBytes(entry.Text));
                Writer.WriteBytes(stream, [0, 0, 0, 0]); // Null termination with two UTF16 characters
            }
        }
        
        // Update the offsets to real ones
        stream.Position = offsetListPosition;
        foreach (long offset in realOffset)
        {
            Writer.WriteUInt32(stream, (uint)offset, ByteOrder.LittleEndian);
        }

        // Write footer
        Writer.WriteUInt16(stream, 0, ByteOrder.LittleEndian);

        // Update the LOCL size
        stream.Position = loclSizePosition;
        Writer.WriteUInt32(stream, (uint)(stream.Length - loclPosition), ByteOrder.LittleEndian);
    }

    private struct UnknwonValues
    {
        public uint LOCH0;
        public uint LOCH1;
        public uint LOCT0;
        public uint LOCT1;
        public uint LOCL0;
    }

    private struct HashData
    {
        public uint Hash;
        public uint Id; // The index of the text entry in LOCL
    }

    private struct TextEntry
    {
        public uint Hash; // Invalid if not using LOCT
        public string Text;
    }
}