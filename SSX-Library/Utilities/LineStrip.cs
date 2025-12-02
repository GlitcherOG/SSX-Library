using System.Numerics;

namespace SSX_Library.Utilities;

/// <summary>
/// Utilities for line strips.
/// </summary>
public static class LineStrip
{
    /// <summary>
    /// Get the Distance of a line strip
    /// </summary>
    public static float Distance(Vector3[] points)
    {
        float distance = 0;
        for (int i = 1; i < points.Length; i++)
            distance += Vector3.Distance(points[i - 1], points[i]);
        return distance;
    }
} 