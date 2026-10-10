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

    /// <summary>
    /// Do two axis-aligned rectangles overlap in the XY plane? Tested by checking whether any corner of
    /// either rectangle falls inside the other. Used by the world-grid (.ltg) rebuild to list a patch or
    /// light in every cell it covers, so collision/lighting does not drop out over the parts of a body
    /// that extend past its centre cell.
    /// </summary>
    public static bool IntersectingSquares(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
    {
        Vector3 a1 = aMin;
        Vector3 a2 = new Vector3(aMax.X, aMin.Y, 0);
        Vector3 a3 = aMax;
        Vector3 a4 = new Vector3(aMin.X, aMax.Y, 0);

        Vector3 b1 = bMin;
        Vector3 b2 = new Vector3(bMax.X, bMin.Y, 0);
        Vector3 b3 = bMax;
        Vector3 b4 = new Vector3(bMin.X, bMax.Y, 0);

        // Any corner of A inside B?
        if (IntersectsPoint(a1, bMin, bMax)) return true;
        if (IntersectsPoint(a3, bMin, bMax)) return true;
        if (IntersectsPoint(a2, bMin, bMax)) return true;
        if (IntersectsPoint(a4, bMin, bMax)) return true;

        // Any corner of B inside A?
        if (IntersectsPoint(b1, aMin, aMax)) return true;
        if (IntersectsPoint(b2, aMin, aMax)) return true;
        if (IntersectsPoint(b3, aMin, aMax)) return true;
        if (IntersectsPoint(b4, aMin, aMax)) return true;

        return false;
    }
}