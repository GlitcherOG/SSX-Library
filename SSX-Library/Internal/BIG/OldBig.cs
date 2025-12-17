using SSX_Library.Utilities;


namespace SSX_Library.Internal.BIG;


/// <summary>
/// Handles BigF, Big4, and COFB big types.
/// </summary>
internal class OldBig
{
    private BigType bigType;
    private IntegerType _bigHeaderIntegerType;
    private IntegerType _MemberFIleHeaderIntegerType;
    private BIGHeader _bigHeader;
    private List<MemberFIleHeader> _memberFiles = [];




    private struct BIGHeader
    {
        public byte[] magic; // Size 2 for COFB, Size 4 for BIGF/BIG4
        public int footerOffset;  // Relative to this value's end
        public int fileCount;
        public MemberFIleHeader[] files;
    }

    private struct MemberFIleHeader
    {
        public int offset; // Position of file data
        public int size; // Size of file data
        public string path; // null terminated
    }

    private struct MemberFileData
    {
        public byte[] data;
    }

    private enum IntegerType { uint16, uint24, uint32}
}