namespace SSX_Library.Internal.Extensions;

/*
    Provides extension methods for the Stream class.
    For functions that dont fit neatly into the Reader & Writer Utillity classes.
*/
internal static class StreamExtensions
{
    //With how often align by 16 is used just better to have a quick function for it

    /// <summary>
    /// Advanced the stream position to the next multiple of 16.
    /// </summary>
    /// <param name="alignment">How many bytes to align by</param>
    public static void AlignBy16(this Stream stream)
    {
        AlignBy(stream, 16);
    }

    /// <summary>
    /// Advanced the stream position to the next multiple of the specified alignment.
    /// Along with including a possible start offset if the start of the alignment
    /// shouldn't be based on beginning of the stream.
    /// </summary>
    /// <param name="alignment">How many bytes to align by</param>
    public static void AlignBy(this Stream stream, int alignment, long startOffset = 0)
    {
        long streamOffset = stream.Position - startOffset;

        int offset = alignment - ((int)streamOffset % alignment);
        if (offset != alignment)
        {
            stream.Position += offset;
        }
    }
}