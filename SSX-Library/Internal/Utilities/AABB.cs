using System.Numerics;

namespace SSX_Library.Internal.Utilities;

/// <summary>
/// Utilities for Bounding Boxes.
/// </summary>
internal static class AABB
{
    /// <summary>
    /// Is point inside Bunding box
    /// </summary>
    public static bool IntersectsPoint(Vector3 point, Vector3 min, Vector3 max)
    {
        return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    }

    /// <summary>
    /// Are two Bunding boxes intersecting.
    /// </summary>
    public static bool IntersectsAABB(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
    {
        return !(aMin.X <= bMax.X && aMax.X >= bMin.X &&
                 aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                 aMin.Z <= bMax.Z && aMax.Z >= bMin.Z);
    }
} 