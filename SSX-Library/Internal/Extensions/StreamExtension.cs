namespace SSX_Library.Internal.Extensions;

/*
    Provides extension methods for the Stream class.
    Functions that dont fit neatly into the Reader & Writer Util classes.
*/
internal static class StreamExtensions
{
    /// <summary>
    /// Advanced the stream position to the next multiple of the specified alignment.
    /// </summary>
    /// <param name="alignment">How many bytes to align by</param>
    public static void AlignBy(this Stream stream, int alignment)
    {
        int offset = alignment - ((int)stream.Position % alignment);
        if (offset != alignment)
        {
            stream.Position += offset;
        }
    }
}