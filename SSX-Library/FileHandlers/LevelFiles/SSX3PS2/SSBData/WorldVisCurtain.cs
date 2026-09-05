using SSX_Library.Internal.Utilities;
using SSXLibrary.JsonFiles.SSX3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData
{
    // Chunk ID-11: visibility curtain. 208 bytes, fixed (the size histogram on
    // bam.ssb is a single bucket, {208: 167}).
    //
    // One record is a planar occluder quad plus its bounding volume: four
    // homogeneous corners (w = 1), the unit plane those corners lie in, a
    // bounding sphere whose centre also lies in that plane, and an AABB. All
    // of it is in the location's own world frame.
    //
    // There are no visibility-cell or portal ids in the record -- every dword
    // is geometry, a constant, or zero padding -- so the occlusion has to be
    // resolved spatially rather than through a stored cell graph.
    //
    // Read order is unchanged; the fields were previously named U0..U7 and
    // Point4/Point3/Point2/ControlPoint.
    public class WorldVisCurtain
    {
        // XYZ = centre, W = radius. The radius is exactly the distance to the
        // furthest corner, and the centre lies on the quad's plane.
        public Vector4 BoundSphere;

        // The occluder quad, in file order. W is 1.0 in every record.
        public Vector4 Corner0;
        public Vector4 Corner1;
        public Vector4 Corner2;
        public Vector4 Corner3;

        // The plane the quad lies in, as n.x + d = 0. The normal is unit length
        // and parallel to the quad's own cross-product normal in every record.
        public Vector3 PlaneNormal;
        public float PlaneDistance;

        public Vector3 BBoxMin;
        public Vector3 BBoxMax;

        // Offset 184. Reads 1 in every record on the retail disc, so its
        // meaning is a guess -- kept because a 208-byte record should not have
        // an unread field.
        public int Flag;

        public void LoadData(Stream stream)
        {
            BoundSphere = StreamUtil.ReadVector4(stream);

            Corner0 = StreamUtil.ReadVector4(stream);
            Corner1 = StreamUtil.ReadVector4(stream);
            Corner2 = StreamUtil.ReadVector4(stream);
            Corner3 = StreamUtil.ReadVector4(stream);

            PlaneNormal = StreamUtil.ReadVector3(stream);
            PlaneDistance = StreamUtil.ReadFloat(stream);

            // Offsets 96-159: zero in every record.
            stream.Position += 0x40;

            BBoxMin = StreamUtil.ReadVector3(stream);
            BBoxMax = StreamUtil.ReadVector3(stream);

            Flag = StreamUtil.ReadInt32(stream);

            // Offsets 188-207 are zero padding to the end of the record.
        }

        public VisCurtainJsonHandler.VisCurtain ToJSON()
        {
            VisCurtainJsonHandler.VisCurtain visCurtain = new VisCurtainJsonHandler.VisCurtain();

            visCurtain.BoundSphere = ArrayConv.Vector4ToArray(BoundSphere);

            visCurtain.Corner0 = ArrayConv.Vector4ToArray(Corner0);
            visCurtain.Corner1 = ArrayConv.Vector4ToArray(Corner1);
            visCurtain.Corner2 = ArrayConv.Vector4ToArray(Corner2);
            visCurtain.Corner3 = ArrayConv.Vector4ToArray(Corner3);

            visCurtain.PlaneNormal = ArrayConv.Vector3ToArray(PlaneNormal);
            visCurtain.PlaneDistance = PlaneDistance;

            visCurtain.BBoxMin = ArrayConv.Vector3ToArray(BBoxMin);
            visCurtain.BBoxMax = ArrayConv.Vector3ToArray(BBoxMax);

            visCurtain.Flag = Flag;

            return visCurtain;
        }
    }
}
