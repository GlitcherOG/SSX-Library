using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSX_Library.EATextureLibrary
{
    internal class EADecode
    {
        //PS2
        //1 (4 Bit, 16 Colour Index)
        //2 (8 Bit, 256 Colour Index)
        //5 (Full Colour)
        //130 is a header flag, will pull out so we can uncompress data easily
        //130 (8 bit, 256 Colour Index Compressed)

        //Xbox 360
        //96 - BCnEncoder.Shared.CompressionFormat.Bc1
        //97 - BCnEncoder.Shared.CompressionFormat.Bc2
        //109 - ImageFormats.BGRA4444 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L289
        //120 - ImageFormats.BGR565 https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L311
        //123 - Indexed Image https://github.com/bartlomiejduda/EA-Graphics-Manager/blob/c9aec00c005437ddbc2752001913e1e2f46840e7/src/EA_Image/ea_image_decoder.py#L334
        //125 - BCnEncoder.Shared.CompressionFormat.Bgra

        //Nintendo Wii/GC
    }
}
