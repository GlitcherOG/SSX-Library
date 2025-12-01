using System.Numerics;

namespace SSX_Library.Utilities;

public class JsonUtil
{
    public static Vector3 Vector4ToVector3(Vector4 vector4)
    {
        return new Vector3(vector4.X, vector4.Y, vector4.Z);
    }

    public static Vector4 Vector3ToVector4(Vector3 vector3, float W = 1)
    {
        return new Vector4(vector3.X, vector3.Y, vector3.Z, W);
    }

    public static float[] Vector4ToArray(Vector4 vector4)
    {
        return [vector4.X, vector4.Y, vector4.Z, vector4.W];
    }

    public static Vector4 ArrayToVector4(float[] floats)
    {
        return new Vector4(floats[0], floats[1], floats[2], floats[3]);
    }

    public static float[] Vector3ToArray(Vector3 vector3)
    {
        return [vector3.X, vector3.Y, vector3.Z];
    }

    public static Vector3 ArrayToVector3(float[] floats)
    {
        return new Vector3(floats[0], floats[1], floats[2]);
    }

    public static Vector3 Array2DToVector3(float[,] floats, int ArrayPos)
    {
        return new Vector3(floats[ArrayPos, 0], floats[ArrayPos,1], floats[ArrayPos,2]);
    }

    public static float[,] Vector3ToArray2D(float[,] floats, Vector3 vector3, int ArrayPos)
    {
        floats[ArrayPos, 0] = vector3.X;
        floats[ArrayPos, 1] = vector3.Y;
        floats[ArrayPos, 2] = vector3.Z;
        return floats;
    }

    public static float[] Vector2ToArray(Vector2 vector3)
    {
        return [vector3.X, vector3.Y];
    }

    public static Vector2 ArrayToVector2(float[] floats)
    {
        return new Vector2(floats[0], floats[1]);
    }

    public static float[] QuaternionToArray(Quaternion quaternion)
    {
        return [quaternion.X, quaternion.Y, quaternion.Z, quaternion.W];
    }

    public static Quaternion ArrayToQuaternion(float[] array)
    {
        return new Quaternion(array[0], array[1], array[2], array[3]);
    }

    /// <summary>
    /// Get the Distance of a line strip
    /// </summary>
    public static float GenerateDistance(Vector3[] points)
    {
        float distance = 0;
        for (int i = 1; i < points.Length; i++)
            distance += Vector3.Distance(points[i - 1], points[i]);
        return distance;
    }

    /// <summary>
    /// Is object inside a Bunding box
    /// </summary>
    public static bool WithinXY(Vector3 point, Vector3 min, Vector3 max)
    {
        return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    }

    //Square 1 is MainPatch. Square 2 is Light
    public static bool IntersectingSquares(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
    {
        return !(aMin.X <= bMax.X && aMax.X >= bMin.X &&
                 aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                 aMin.Z <= bMax.Z && aMax.Z >= bMin.Z);
    }
}
