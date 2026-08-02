using SSX_Library.Internal.Utilities;
using SSXLibrary.JsonFiles.SSX3;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.SSX3PS2.SSBData
{
    // Chunk ID-5: particle-FX placement instance. 144 bytes, fixed (the size
    // histogram on bam.ssb is a single bucket, {144: 141}).
    //
    // Each record places one particle effect: a rigid world transform, the
    // effect's bounding sphere and AABB, and the ObjectID of the ID-4 particle
    // model it draws. ID-4 and ID-5 share one ObjectID namespace and appear 1:1
    // in every location that has them, so ObjectID is both this record's own id
    // and the join key to its model.
    //
    // Read order is unchanged from the previous WorldBin5 -- 4 int32, four
    // Vector4 rows (one Matrix4x4), a Vector4, two int32, two Vector3, 4 int32 --
    // so this is a rename, not a reinterpretation.
    public class WorldParticleInstance
    {
        // Offsets 0-15 and 128-143. Zero in all 141 records on the retail disc.
        public int[] Reserved0 = new int[4];
        public int[] Reserved1 = new int[4];

        // World placement. Rows 0-2 are a proper rotation (determinant 1, no
        // scale or shear); row 3 is the translation. Particle size lives in the
        // ID-4 model, not here.
        public Matrix4x4 Transform = new Matrix4x4();

        // XYZ = centre, W = radius. The centre sits inside AABBMin/AABBMax in
        // every record, and equals the translation plus a per-model constant
        // offset, so these bounds are the model's local volume carried into
        // world space.
        public Vector4 BoundingSphere;

        // Self id, and the key to the paired ID-4 particle model. Stored twice;
        // the second copy is byte-identical in all 141 records.
        public ObjectID objectID;
        public ObjectID ObjectIDDuplicate;

        public Vector3 AABBMin;
        public Vector3 AABBMax;

        public void LoadData(Stream stream)
        {
            for (int i = 0; i < 4; i++)
            {
                Reserved0[i] = StreamUtil.ReadInt32(stream);
            }

            Transform = StreamUtil.ReadMatrix4x4(stream);

            BoundingSphere = StreamUtil.ReadVector4(stream);

            objectID = WorldCommon.ObjectIDLoad(stream);
            ObjectIDDuplicate = WorldCommon.ObjectIDLoad(stream);

            AABBMin = StreamUtil.ReadVector3(stream);
            AABBMax = StreamUtil.ReadVector3(stream);

            for (int i = 0; i < 4; i++)
            {
                Reserved1[i] = StreamUtil.ReadInt32(stream);
            }
        }

        public ParticleInstanceJsonHandler.ParticleInstance ToJSON()
        {
            ParticleInstanceJsonHandler.ParticleInstance particleInstance = new ParticleInstanceJsonHandler.ParticleInstance();

            particleInstance.Reserved0 = Reserved0;
            particleInstance.Reserved1 = Reserved1;

            Vector3 Scale;
            Quaternion Rotation;
            Vector3 Location;

            Matrix4x4.Decompose(Transform, out Scale, out Rotation, out Location);
            particleInstance.Position = ArrayConv.Vector3ToArray(Location);
            particleInstance.Rotation = ArrayConv.QuaternionToArray(Rotation);
            particleInstance.Scale = ArrayConv.Vector3ToArray(Scale);

            particleInstance.BoundingSphere = ArrayConv.Vector4ToArray(BoundingSphere);

            particleInstance.TrackID = objectID.TrackID;
            particleInstance.RID = objectID.RID;
            particleInstance.DuplicateTrackID = ObjectIDDuplicate.TrackID;
            particleInstance.DuplicateRID = ObjectIDDuplicate.RID;

            particleInstance.AABBMin = ArrayConv.Vector3ToArray(AABBMin);
            particleInstance.AABBMax = ArrayConv.Vector3ToArray(AABBMax);

            return particleInstance;
        }
    }
}
