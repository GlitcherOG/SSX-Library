using System.Numerics;

namespace SSX_Library.Utilities;

public static class Euler
{
    public static Quaternion ToQuaternion(Vector3 euler)
    {
        float cy = (float)Math.Cos(euler.Z * 0.5);
        float sy = (float)Math.Sin(euler.Z * 0.5);
        float cp = (float)Math.Cos(euler.Y * 0.5);
        float sp = (float)Math.Sin(euler.Y * 0.5);
        float cr = (float)Math.Cos(euler.X * 0.5);
        float sr = (float)Math.Sin(euler.X * 0.5);

        return new Quaternion
        {
            W = cr * cp * cy + sr * sp * sy,
            X = sr * cp * cy - cr * sp * sy,
            Y = cr * sp * cy + sr * cp * sy,
            Z = cr * cp * sy - sr * sp * cy
        };
    }

    public static Vector3 FromQuaternion(Quaternion q)
    {
        Vector3 angles = new();

        // roll / x
        double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        angles.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

        // pitch / y
        double sinp = 2 * (q.W * q.Y - q.Z * q.X);
        if (Math.Abs(sinp) >= 1)
        {
            angles.Y = (float)Math.CopySign(Math.PI / 2, sinp);
        }
        else
        {
            angles.Y = (float)Math.Asin(sinp);
        }

        // yaw / z
        double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        angles.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

        return angles;
    }
}
