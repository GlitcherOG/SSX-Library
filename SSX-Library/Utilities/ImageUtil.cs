using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SSXLibrary.Utilities
{
    public class ImageUtil
    {
        public static HashSet<Rgba32> GetBitmapColorsFast(Image<Rgba32> bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            HashSet<Rgba32> result = new HashSet<Rgba32>();

            // Grab raw pixel rows
            for (int y = 0; y < height; y++)
            {
                Span<Rgba32> row = bmp.DangerousGetPixelRowMemory(y).Span;

                for (int x = 0; x < width; x++)
                {
                    result.Add(row[x]);
                }
            }

            return result;
        }

        public static Image<Rgba32> ReduceBitmapColorsFast(Image<Rgba32> img, int maxColors)
        {
            int width = img.Width;
            int height = img.Height;

            // Step 1: Extract sampled pixel colors (25% sampling like your code)
            var pixelColors = new List<Rgba32>(width * height / 4);

            for (int y = 0; y < height; y += 2)
            {
                Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;

                for (int x = 0; x < width; x += 2)
                {
                    pixelColors.Add(row[x]);
                }
            }

            // Step 2: Compute reduced palette
            var reducedPalette = ReduceColors(pixelColors, maxColors);

            // Step 3: Recolor image using nearest palette color (parallel)
            Parallel.For(0, height, y =>
            {
                Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;

                for (int x = 0; x < width; x++)
                {
                    var nearest = FindNearestColor(row[x], reducedPalette);

                    row[x] = new Rgba32(nearest.R, nearest.G, nearest.B, nearest.A);
                }
            });

            return img;
        }

        // --- Helper methods below ---

        public static List<Rgba32> ReduceColors(List<Rgba32> inputColors, int maxColors)
        {
            if (inputColors.Count <= maxColors)
                return inputColors.Distinct().ToList();

            var vectors = inputColors.Select(c => new float[] { c.R, c.G, c.B }).ToList();
            var clusters = KMeans(vectors, maxColors);
            return clusters.Select(v => new Rgba32(
                (int)Math.Round(v[0]),
                (int)Math.Round(v[1]),
                (int)Math.Round(v[2])
            )).ToList();
        }

        private static List<float[]> KMeans(List<float[]> points, int k, int maxIterations = 20)
        {
            var rnd = new Random();
            var centroids = points.OrderBy(_ => rnd.Next()).Take(k).ToList();
            var assignments = new int[points.Count];

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                Parallel.For(0, points.Count, i =>
                {
                    float minDist = float.MaxValue;
                    int closest = 0;
                    for (int j = 0; j < k; j++)
                    {
                        float dist = Distance(points[i], centroids[j]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            closest = j;
                        }
                    }
                    assignments[i] = closest;
                });

                var newCentroids = new List<float[]>(k);
                for (int j = 0; j < k; j++)
                    newCentroids.Add(new float[3]);

                int[] counts = new int[k];
                for (int i = 0; i < points.Count; i++)
                {
                    int cluster = assignments[i];
                    counts[cluster]++;
                    newCentroids[cluster][0] += points[i][0];
                    newCentroids[cluster][1] += points[i][1];
                    newCentroids[cluster][2] += points[i][2];
                }

                for (int j = 0; j < k; j++)
                {
                    if (counts[j] > 0)
                    {
                        newCentroids[j][0] /= counts[j];
                        newCentroids[j][1] /= counts[j];
                        newCentroids[j][2] /= counts[j];
                    }
                    else
                    {
                        newCentroids[j] = points[rnd.Next(points.Count)];
                    }
                }

                centroids = newCentroids;
            }

            return centroids;
        }

        private static float Distance(float[] a, float[] b)
        {
            float dr = a[0] - b[0];
            float dg = a[1] - b[1];
            float db = a[2] - b[2];
            return dr * dr + dg * dg + db * db;
        }

        private static Rgba32 FindNearestColor(Rgba32 color, List<Rgba32> palette)
        {
            int minDist = int.MaxValue;
            Rgba32 nearest = palette[0];

            foreach (var p in palette)
            {
                int dr = color.R - p.R;
                int dg = color.G - p.G;
                int db = color.B - p.B;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = p;
                }
            }

            return nearest;
        }
    }
}
