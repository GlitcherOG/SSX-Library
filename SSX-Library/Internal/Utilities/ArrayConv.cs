using System.Diagnostics;
using System.Numerics;

namespace SSX_Library.Internal.Utilities;

/// <summary>
/// Converts values to/from arrays.
/// </summary>
internal static class ArrayConv
{
    public static float[] Vector4ToArray(Vector4 vector4)
    {
        return [vector4.X, vector4.Y, vector4.Z, vector4.W];
    }

    public static Vector4 ArrayToVector4(float[] floats)
    {
        Debug.Assert(floats.Length >= 4, "Not enough floats passed");
        return new Vector4(floats[0], floats[1], floats[2], floats[3]);
    }

    public static float[] Vector3ToArray(Vector3 vector3)
    {
        return [vector3.X, vector3.Y, vector3.Z];
    }

    public static Vector3 ArrayToVector3(float[] floats)
    {
        Debug.Assert(floats.Length >= 3, "Not enough floats passed");
        return new Vector3(floats[0], floats[1], floats[2]);
    }

    public static Vector3 Array2DToVector3(float[,] floats, int ArrayPos)
    {
        Debug.Assert(floats.GetLength(0) == 4, "Multi-Dimentional array is not size [4, 3]");
        Debug.Assert(floats.GetLength(1) == 3, "Multi-Dimentional array is not size [4, 3]");
        return new Vector3(floats[ArrayPos, 0], floats[ArrayPos,1], floats[ArrayPos,2]);
    }

    public static float[,] Vector3ToArray2D(Vector3 vector3, int ArrayPos)
    {
        float[,] array = new float[4,3];
        array[ArrayPos, 0] = vector3.X;
        array[ArrayPos, 1] = vector3.Y;
        array[ArrayPos, 2] = vector3.Z;
        return array;
    }

    public static float[] Vector2ToArray(Vector2 vector2)
    {
        return [vector2.X, vector2.Y];
    }

    public static Vector2 ArrayToVector2(float[] floats)
    {
        Debug.Assert(floats.Length >= 2, "Not enough floats passed");
        return new Vector2(floats[0], floats[1]);
    }

    public static float[] QuaternionToArray(Quaternion quaternion)
    {
        return [quaternion.X, quaternion.Y, quaternion.Z, quaternion.W];
    }

    public static Quaternion ArrayToQuaternion(float[] floats)
    {
        Debug.Assert(floats.Length >= 4, "Not enough floats passed");
        return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
    }
} 