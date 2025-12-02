using System.Numerics;

namespace SSX_Library.Utilities;

/// <summary>
/// Converts between Vector3 and Vector4.
/// </summary>
public static class VectorConv
{
    public static Vector3 Vector4ToVector3(Vector4 vector4)
    {
        return new Vector3(vector4.X, vector4.Y, vector4.Z);
    }

    public static Vector4 Vector3ToVector4(Vector3 vector3, float w = 1)
    {
        return new Vector4(vector3.X, vector3.Y, vector3.Z, w);
    }
}