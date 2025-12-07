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

    // TODO: Replace TextureEntriesCount in Load/Save with the size of the TextEntries list.
    // The user can change the array while its unsynced to the counter.
    public List<string> TextEntries
    {
        get { return _locL.TextEntries;}
        set {_locL.TextEntries = value;}
    }

    /// <summary>
    /// Load an LOC file from disk to memory.
    /// </summary>
    /// <param name="path"> Path to the LOC file on disk.</param>
    public void Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        _filePath = path;

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
            LOCHSize = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            Unk0 = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            Unk1 = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            LOCLOffset = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
        };

        // Check if LOCT signature was found
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

        // Create LOCT
        if (_usesLOCT)
        {
            uint loctSize = _locH.LOCLOffset - (uint)stream.Position;
            _locT = new()
            {
                MagicWord = [.._locTMagicWord],
                Data = Reader.ReadBytes(stream, (int)loctSize),
            };
        }
        else
        {
            stream.Position -= 4; // Go back to re-read the signature for LOCL
        }

        // Check if LOCL signature was found
        var locLPosition = (uint)stream.Position; // Used later on
        var magicLOCL = Reader.ReadBytes(stream, 4);
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
            LOCLSize = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            Unk0 = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            TextEntryCount = Reader.ReadUint32(stream, ByteOrder.LittleEndian),
            TextEntryOffsets = [],
            TextEntries = [],
        };
        for (int i = 0; i < _locL.TextEntryCount; i++)
        {
            _locL.TextEntryOffsets.Add(Reader.ReadUint32(stream, ByteOrder.LittleEndian));
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
    }

    /// <summary>
    /// Save an LOC file from memory to disk.
    /// </summary>
    /// <param name="path">Path to save the LOC. If empty it will save to the same place
    /// it was loaded from. </param>
    public void Save(string path = "")
    {
        if(path == "") path = _filePath;
        using FileStream stream = File.Create(path);

        // Save LOCH
        stream.Write([.._locHMagicWord]);
        var buf4 = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locH.LOCHSize);
        stream.Write(buf4);
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locH.Unk0);
        stream.Write(buf4);
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locH.Unk1);
        stream.Write(buf4);
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locH.LOCLOffset);
        stream.Write(buf4);

        // Save LOCT if used
        if (_usesLOCT)
        {
            stream.Write([.._locTMagicWord]);
            stream.Write(_locT.Data);
        }

        // Save LOCL
        var locLPosition = (uint)stream.Position;
        stream.Write([.._locLMagicWord]);
        stream.Write(buf4); // Placeholder LOCLSize
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locL.Unk0);
        stream.Write(buf4);
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, _locL.TextEntryCount);
        stream.Write(buf4);
        var locLOffsetsPosition = (uint)stream.Position;
        var offsetsPlaceholder = new byte[_locL.TextEntryCount * sizeof(uint)];
        stream.Write(offsetsPlaceholder); // Placeholder offsets
        offsetsPlaceholder = null; // No longer needed
        List<uint> realOffset = [];
        foreach (var textEntry in _locL.TextEntries)
        {
            realOffset.Add((uint)stream.Position);
            byte[] text = Encoding.Unicode.GetBytes(textEntry);
            stream.Write(text);
            stream.Write([0, 0, 0, 0]); // Null termination with two UTF16 characters
        }
        uint realLOCLSize = locLPosition - (uint)stream.Position;
        // Rewrite the offsets to real ones
        stream.Position = locLOffsetsPosition;
        for (int i = 0; i < _locL.TextEntryCount; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf4, realOffset[i]);
            stream.Write(buf4);
        }
        // Set LOCL size
        stream.Position = locLPosition + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(buf4, (uint)stream.Length - locLPosition + _locH.LOCHSize);
        stream.Write(buf4);
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
        public List<string> TextEntries; // Length: TextEntryCount
    }
}