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
        var lastPoint = points[0];
        for (int i = 1; i < points.Length; i++)
        {
            distance += Vector3.Distance(lastPoint, points[i]);
            lastPoint = points[i];
        }
        return distance;
    }

    public static bool WithinXY(Vector3 Object, Vector3 LowestXYZ, Vector3 HighestXYZ)
    {
        if (Object.X >= LowestXYZ.X&& Object.X <= HighestXYZ.X && Object.Y >= LowestXYZ.Y && Object.Y <= HighestXYZ.Y)
        {
            return true;
        }
        return false;
    }

    //Square 1 is MainPatch. Square 2 is Light
    public static bool IntersectingSquares(Vector3 Square1Lowest, Vector3 Square1Highest, Vector3 Square2Lowest, Vector3 Square2Highest)
    {
        Vector3 Square1Point1 = Square1Lowest;
        Vector3 Square1Point2 = new Vector3(Square1Highest.X, Square1Lowest.Y, 0);
        Vector3 Square1Point3 = Square1Highest;
        Vector3 Square1Point4 = new Vector3(Square1Lowest.X, Square1Highest.Y, 0);

        Vector3 Square2Point1 = Square2Lowest;
        Vector3 Square2Point2 = new Vector3(Square2Highest.X, Square2Lowest.Y, 0);
        Vector3 Square2Point3 = Square2Highest;
        Vector3 Square2Point4 = new Vector3(Square2Lowest.X, Square2Highest.Y, 0);

        //Check if Node is within Light
        if (Square1Point1.X >= Square2Lowest.X && Square1Point1.X <= Square2Highest.X && Square1Point1.Y >= Square2Lowest.Y && Square1Point1.Y <= Square2Highest.Y)
        {
            return true;
        }

        if (Square1Point3.X >= Square2Lowest.X && Square1Point3.X <= Square2Highest.X && Square1Point3.Y >= Square2Lowest.Y && Square1Point3.Y <= Square2Highest.Y)
        {
            return true;
        }

        if (Square1Point2.X >= Square2Lowest.X && Square1Point2.X <= Square2Highest.X && Square1Point2.Y >= Square2Lowest.Y && Square1Point2.Y <= Square2Highest.Y)
        {
            return true;
        }

        if (Square1Point4.X >= Square2Lowest.X && Square1Point4.X <= Square2Highest.X && Square1Point4.Y >= Square2Lowest.Y && Square1Point4.Y <= Square2Highest.Y)
        {
            return true;
        }

        //Check if Light is Within Node
        if (Square2Point1.X >= Square1Lowest.X && Square2Point1.X <= Square1Highest.X && Square2Point1.Y >= Square1Lowest.Y && Square2Point1.Y <= Square1Highest.Y)
        {
            return true;
        }

        if (Square2Point2.X >= Square1Lowest.X && Square2Point2.X <= Square1Highest.X && Square2Point2.Y >= Square1Lowest.Y && Square2Point2.Y <= Square1Highest.Y)
        {
            return true;
        }

        if (Square2Point3.X >= Square1Lowest.X && Square2Point3.X <= Square1Highest.X && Square2Point3.Y >= Square1Lowest.Y && Square2Point3.Y <= Square1Highest.Y)
        {
            return true;
        }

        if (Square2Point4.X >= Square1Lowest.X && Square2Point4.X <= Square1Highest.X && Square2Point4.Y >= Square1Lowest.Y && Square2Point4.Y <= Square1Highest.Y)
        {
            return true;
        }

        return false;
    }
}
