// WARNING: Claude (AI) was used to write this lightmap converter. It was given ideas from our own WIP code,
// which we had issues getting working correctly due to floating point image math.

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SSXLibrary.JsonFiles.Tricky;
using System.Numerics;

namespace SSXLibrary.FileHandlers.LevelFiles.Tricky
{
    // PS2 Tricky lights patches with (texture - lightmap.rgb) * lightmap.a. Xbox uses texture * lightmap.
    // Both directions sample the texture that sits under each lightmap texel, work out what the source format renders
    // there, and solve for the lightmap in the other format that reproduces it best. A multiply can't match a subtract at
    // every texture pixel (or the other way round), so each patch's lightmap rect is a least squares fit over the
    // texture it covers (exact where that texture is a flat colour).
    //
    // Mapping follows Ice Saw's TessellatedPatch: patch (s,t) -> lightmap (X + Z*s, Y + W*t), texture UV is a bilinear
    // blend of UVPoints (0 = (0,0), 1 = (0,1), 2 = (1,0), 3 = (1,1)), the lightmap is filtered bilinearly and clamped
    // to the patch's own rect (Ice Saw's padded tiles), the texture is filtered bilinearly and repeats.
    // PS2 lightmap alpha is used as the raw byte / 255.
    public static class TrickyLightmapConverter
    {
        public static int MinSamplesPerTexel = 2;      // Per axis, per lightmap texel
        public static int MaxSamplesPerTexel = 12;
        public static double Regularization = 0.001;   // Pull toward the per texel formula where the texture gives little to fit against
        public static double TrickyRegularization = 0.1; // Same for ConvertToTricky. Higher because a and L can trade off under flat textures (0.03-0.1 fit best on Gari)
        public static bool DebugOutput = false;        // Writes Debug/ images: texture projected into the page, PS2 render, standard render
        public static int DebugScale = 4;

        // PS2 (texture - L) * a -> standard texture * X
        public static void ConvertToStandard(PatchesJsonHandler Patches, string LightmapPath, string TexturePath, string OutputPath)
        {
            Convert(Patches, LightmapPath, TexturePath, OutputPath, true);
        }

        // Standard texture * X -> PS2 (texture - L) * a
        public static void ConvertToTricky(PatchesJsonHandler Patches, string LightmapPath, string TexturePath, string OutputPath)
        {
            Convert(Patches, LightmapPath, TexturePath, OutputPath, false);
        }

        static void Convert(PatchesJsonHandler Patches, string LightmapPath, string TexturePath, string OutputPath, bool toStandard)
        {
            string label = toStandard ? "Lightmap To Standard: " : "Lightmap To Tricky: ";

            var pages = new List<FloatImage>();
            while (File.Exists(PagePath(LightmapPath, pages.Count)))
            {
                pages.Add(FloatImage.Load(PagePath(LightmapPath, pages.Count)));
            }
            if (pages.Count == 0)
            {
                Console.WriteLine(label + "no pages in " + LightmapPath);
                return;
            }

            var textures = new Dictionary<string, FloatImage?>();
            var groups = new Dictionary<(int, int, int, int, int), RectGroup>();
            for (int i = 0; i < Patches.Patches.Count; i++)
            {
                var patch = Patches.Patches[i];
                if (patch.LightMapPoint == null || patch.UVPoints == null || patch.LightmapID < 0 || patch.LightmapID >= pages.Count)
                {
                    continue;
                }

                var page = pages[patch.LightmapID];
                int x0 = Math.Clamp((int)Math.Round(patch.LightMapPoint[0] * page.Width), 0, page.Width);
                int y0 = Math.Clamp((int)Math.Round(patch.LightMapPoint[1] * page.Height), 0, page.Height);
                int x1 = Math.Clamp((int)Math.Round((patch.LightMapPoint[0] + patch.LightMapPoint[2]) * page.Width), 0, page.Width);
                int y1 = Math.Clamp((int)Math.Round((patch.LightMapPoint[1] + patch.LightMapPoint[3]) * page.Height), 0, page.Height);
                if (x1 <= x0 || y1 <= y0)
                {
                    continue;
                }

                var key = (patch.LightmapID, x0, y0, x1 - x0, y1 - y0);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new RectGroup { Page = patch.LightmapID, X = x0, Y = y0, W = x1 - x0, H = y1 - y0 };
                    groups.Add(key, group);
                }
                group.Patches.Add(i);

                string texture = patch.TexturePath ?? "";
                if (!textures.ContainsKey(texture))
                {
                    string path = Path.Combine(TexturePath, texture);
                    textures[texture] = texture != "" && File.Exists(path) ? FloatImage.Load(path) : null;
                }
            }

            var groupList = groups.Values.ToList();
            var results = new GroupResult[groupList.Count];
            Parallel.For(0, groupList.Count, g =>
            {
                var group = groupList[g];
                var page = pages[group.Page];
                var samples = GatherSamples(group, Patches, page, textures);
                results[g] = toStandard ? SolveStandard(group, samples, page) : SolveTricky(group, samples, page);
            });

            // Texels no patch uses get the per texel formula
            var output = new List<FloatImage>();
            for (int p = 0; p < pages.Count; p++)
            {
                var src = pages[p];
                var dst = new FloatImage(src.Width, src.Height);
                var texel = new double[4];
                for (int i = 0; i < src.Width * src.Height; i++)
                {
                    if (toStandard)
                    {
                        StandardPrior(src.Data[i * 4], src.Data[i * 4 + 1], src.Data[i * 4 + 2], src.Data[i * 4 + 3], texel);
                    }
                    else
                    {
                        TrickyPrior(src.Data[i * 4], src.Data[i * 4 + 1], src.Data[i * 4 + 2], texel);
                    }
                    for (int c = 0; c < 4; c++)
                    {
                        dst.Data[i * 4 + c] = (float)texel[c];
                    }
                }
                output.Add(dst);
            }

            var pageFit = new double[pages.Count];
            var pageBase = new double[pages.Count];
            var pageWeight = new double[pages.Count];
            for (int g = 0; g < groupList.Count; g++)
            {
                var group = groupList[g];
                var result = results[g];
                var dst = output[group.Page];
                for (int y = 0; y < group.H; y++)
                {
                    for (int x = 0; x < group.W; x++)
                    {
                        int k = y * group.W + x;
                        int d = ((group.Y + y) * dst.Width + group.X + x) * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            dst.Data[d + c] = (float)result.Texels[c][k];
                        }
                    }
                }
                pageFit[group.Page] += result.FitError;
                pageBase[group.Page] += result.BaseError;
                pageWeight[group.Page] += result.Weight;
            }

            Directory.CreateDirectory(OutputPath);
            string baseName = toStandard ? "a*(1-L)" : "per texel";
            double totalFit = 0, totalBase = 0, totalWeight = 0;
            for (int p = 0; p < output.Count; p++)
            {
                output[p].Save(PagePath(OutputPath, p));
                if (pageWeight[p] > 0)
                {
                    Console.WriteLine(label + (p + 1) + "/" + output.Count + " render error (0-255 RMS) " + baseName + " " +
                        Rms(pageBase[p], pageWeight[p]).ToString("0.00") + " -> fitted " + Rms(pageFit[p], pageWeight[p]).ToString("0.00"));
                }
                totalFit += pageFit[p];
                totalBase += pageBase[p];
                totalWeight += pageWeight[p];
            }
            Console.WriteLine(label + "total render error (0-255 RMS) " + baseName + " " + Rms(totalBase, totalWeight).ToString("0.00") +
                " -> fitted " + Rms(totalFit, totalWeight).ToString("0.00"));

            if (DebugOutput)
            {
                WriteDebug(Path.Combine(OutputPath, "Debug"), groupList, Patches, toStandard ? pages : output, toStandard ? output : pages, textures);
            }
        }

        // PS2 -> standard. Per channel normal equations M x = b, minimising sum(weight * (t * X - ps2)^2)
        static GroupResult SolveStandard(RectGroup group, Sample[] samples, FloatImage page)
        {
            int n = group.W * group.H;
            var L = new double[3][];
            var A = new double[n];
            var prior = new double[4][];
            for (int c = 0; c < 4; c++)
            {
                prior[c] = new double[n];
            }
            for (int c = 0; c < 3; c++)
            {
                L[c] = new double[n];
            }
            var texel = new double[4];
            for (int y = 0; y < group.H; y++)
            {
                for (int x = 0; x < group.W; x++)
                {
                    int k = y * group.W + x;
                    int s = ((group.Y + y) * page.Width + group.X + x) * 4;
                    A[k] = page.Data[s + 3];
                    for (int c = 0; c < 3; c++)
                    {
                        L[c][k] = page.Data[s + c];
                    }
                    StandardPrior(L[0][k], L[1][k], L[2][k], A[k], texel);
                    for (int c = 0; c < 4; c++)
                    {
                        prior[c][k] = texel[c];
                    }
                }
            }

            var M = new double[3][];
            var B = new double[3][];
            var rr = new double[3];
            double weightSum = 0;
            for (int c = 0; c < 3; c++)
            {
                M[c] = new double[n * n];
                B[c] = new double[n];
            }

            Span<int> idx = stackalloc int[4];
            Span<double> w = stackalloc double[4];
            foreach (var sample in samples)
            {
                sample.Unpack(idx, w);
                double weight = sample.Weight;
                weightSum += weight;
                double af = Filter(A, sample);
                for (int c = 0; c < 3; c++)
                {
                    double tc = sample.T(c);
                    double ps2 = Math.Clamp((tc - Filter(L[c], sample)) * af, 0.0, 1.0);
                    double tt = weight * tc * tc;
                    double tr = weight * tc * ps2;
                    rr[c] += weight * ps2 * ps2;

                    var Mc = M[c];
                    var Bc = B[c];
                    for (int p = 0; p < 4; p++)
                    {
                        int row = idx[p] * n;
                        double ttw = tt * w[p];
                        Bc[idx[p]] += tr * w[p];
                        for (int q = 0; q < 4; q++)
                        {
                            Mc[row + idx[q]] += ttw * w[q];
                        }
                    }
                }
            }

            var result = new GroupResult { Texels = new double[4][], Weight = weightSum };
            result.Texels[3] = prior[3];
            for (int c = 0; c < 3; c++)
            {
                var x = (double[])prior[c].Clone();
                double lambda = Regularization * Trace(M[c], n, 0) / n;
                if (lambda > 0)
                {
                    var m = (double[])M[c].Clone();
                    var b = (double[])B[c].Clone();
                    for (int k = 0; k < n; k++)
                    {
                        m[k * n + k] += lambda;
                        b[k] += lambda * prior[c][k];
                    }
                    var solved = new double[n];
                    if (CholeskySolve(m, b, n, solved))
                    {
                        for (int k = 0; k < n; k++)
                        {
                            x[k] = Math.Clamp(solved[k], 0.0, 1.0);
                        }
                    }
                }
                result.Texels[c] = x;
                result.FitError += SquaredError(M[c], B[c], rr[c], x, n);
                result.BaseError += SquaredError(M[c], B[c], rr[c], prior[c], n);
            }
            return result;
        }

        // Standard -> PS2. Alpha is shared by all three channels, so a and b = a*L are solved together:
        // (t - L)*a = t*a - a*L, taking filtered(a*L) for filtered(a)*filtered(L), which is linear in [a, b_r, b_g, b_b].
        // The final error is measured with the real PS2 maths.
        static GroupResult SolveTricky(RectGroup group, Sample[] samples, FloatImage page)
        {
            int n = group.W * group.H;
            int size = n * 4;
            var X = new double[3][];
            var prior = new double[4][];
            for (int c = 0; c < 4; c++)
            {
                prior[c] = new double[n];
            }
            for (int c = 0; c < 3; c++)
            {
                X[c] = new double[n];
            }
            var texel = new double[4];
            for (int y = 0; y < group.H; y++)
            {
                for (int x = 0; x < group.W; x++)
                {
                    int k = y * group.W + x;
                    int s = ((group.Y + y) * page.Width + group.X + x) * 4;
                    for (int c = 0; c < 3; c++)
                    {
                        X[c][k] = page.Data[s + c];
                    }
                    TrickyPrior(X[0][k], X[1][k], X[2][k], texel);
                    for (int c = 0; c < 4; c++)
                    {
                        prior[c][k] = texel[c];
                    }
                }
            }

            // Blocks of the normal matrix: a-a, b-b (same for every channel), a-b per channel
            var Saa = new double[n * n];
            var Sbb = new double[n * n];
            var Sab = new double[3][];
            var ga = new double[n];
            var gb = new double[3][];
            for (int c = 0; c < 3; c++)
            {
                Sab[c] = new double[n * n];
                gb[c] = new double[n];
            }

            Span<int> idx = stackalloc int[4];
            Span<double> w = stackalloc double[4];
            Span<double> target = stackalloc double[3];
            double weightSum = 0;
            foreach (var sample in samples)
            {
                sample.Unpack(idx, w);
                double weight = sample.Weight;
                weightSum += weight;
                double tsq = 0, ty = 0;
                for (int c = 0; c < 3; c++)
                {
                    double tc = sample.T(c);
                    target[c] = tc * Filter(X[c], sample);
                    tsq += tc * tc;
                    ty += tc * target[c];
                }
                for (int p = 0; p < 4; p++)
                {
                    int row = idx[p] * n;
                    double wp = weight * w[p];
                    ga[idx[p]] += wp * ty;
                    for (int c = 0; c < 3; c++)
                    {
                        gb[c][idx[p]] -= wp * target[c];
                    }
                    for (int q = 0; q < 4; q++)
                    {
                        double ww = wp * w[q];
                        Saa[row + idx[q]] += ww * tsq;
                        Sbb[row + idx[q]] += ww;
                        for (int c = 0; c < 3; c++)
                        {
                            Sab[c][row + idx[q]] -= ww * sample.T(c);
                        }
                    }
                }
            }

            // Unknowns: a at 0..n-1, b_c at n*(c+1)..
            var u = new double[size];
            for (int k = 0; k < n; k++)
            {
                u[k] = prior[3][k];
                for (int c = 0; c < 3; c++)
                {
                    u[n * (c + 1) + k] = prior[3][k] * prior[c][k];
                }
            }

            double lambdaA = TrickyRegularization * Trace(Saa, n, 0) / n;
            double lambdaB = TrickyRegularization * Trace(Sbb, n, 0) / n;
            if (lambdaA > 0 && lambdaB > 0)
            {
                var m = new double[size * size];
                var rhs = new double[size];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        m[i * size + j] = Saa[i * n + j];
                        for (int c = 0; c < 3; c++)
                        {
                            int o = n * (c + 1);
                            m[(o + i) * size + o + j] = Sbb[i * n + j];
                            m[i * size + o + j] = Sab[c][i * n + j];
                            m[(o + j) * size + i] = Sab[c][i * n + j];
                        }
                    }
                    m[i * size + i] += lambdaA;
                    rhs[i] = ga[i] + lambdaA * u[i];
                    for (int c = 0; c < 3; c++)
                    {
                        int o = n * (c + 1);
                        m[(o + i) * size + o + i] += lambdaB;
                        rhs[o + i] = gb[c][i] + lambdaB * u[o + i];
                    }
                }
                var solved = new double[size];
                if (CholeskySolve(m, rhs, size, solved))
                {
                    u = solved;
                }
            }

            var result = new GroupResult { Texels = new double[4][], Weight = weightSum };
            for (int c = 0; c < 4; c++)
            {
                result.Texels[c] = new double[n];
            }
            for (int k = 0; k < n; k++)
            {
                double a = Math.Clamp(u[k], 0.0, 1.0);
                result.Texels[3][k] = a;
                for (int c = 0; c < 3; c++)
                {
                    double b = Math.Clamp(u[n * (c + 1) + k], 0.0, a);
                    result.Texels[c][k] = a > 0 ? b / a : 0.0;
                }
            }

            result.FitError = TrickyError(samples, result.Texels, X);
            result.BaseError = TrickyError(samples, prior, X);
            return result;
        }

        static double TrickyError(Sample[] samples, double[][] ps2, double[][] X)
        {
            double error = 0;
            foreach (var sample in samples)
            {
                double af = Filter(ps2[3], sample);
                for (int c = 0; c < 3; c++)
                {
                    double tc = sample.T(c);
                    double render = Math.Clamp((tc - Filter(ps2[c], sample)) * af, 0.0, 1.0);
                    double d = render - tc * Filter(X[c], sample);
                    error += sample.Weight * d * d;
                }
            }
            return error;
        }

        // Exact for a white texture
        static void StandardPrior(double r, double g, double b, double a, double[] output)
        {
            output[0] = a * (1.0 - r);
            output[1] = a * (1.0 - g);
            output[2] = a * (1.0 - b);
            output[3] = 1.0;
        }

        // Inverse of StandardPrior
        static void TrickyPrior(double r, double g, double b, double[] output)
        {
            double a = Math.Clamp(Math.Max(r, Math.Max(g, b)), 0.0, 1.0);
            output[0] = a > 0 ? Math.Clamp(1.0 - r / a, 0.0, 1.0) : 0.0;
            output[1] = a > 0 ? Math.Clamp(1.0 - g / a, 0.0, 1.0) : 0.0;
            output[2] = a > 0 ? Math.Clamp(1.0 - b / a, 0.0, 1.0) : 0.0;
            output[3] = a;
        }

        static Sample[] GatherSamples(RectGroup group, PatchesJsonHandler Patches, FloatImage page, Dictionary<string, FloatImage?> textures)
        {
            var samples = new List<Sample>();
            Span<double> tex = stackalloc double[4];
            foreach (int pi in group.Patches)
            {
                var patch = Patches.Patches[pi];
                textures.TryGetValue(patch.TexturePath ?? "", out var texture);
                var uv = UVCorners(patch);

                double lx = patch.LightMapPoint[0] * page.Width;
                double ly = patch.LightMapPoint[1] * page.Height;
                double lw = patch.LightMapPoint[2] * page.Width;
                double lh = patch.LightMapPoint[3] * page.Height;

                int ss = SampleCount(texture, uv[0], uv[2], uv[1], uv[3], group.W);
                int st = SampleCount(texture, uv[0], uv[1], uv[2], uv[3], group.H);
                int ns = group.W * ss;
                int nt = group.H * st;
                double sampleWeight = 1.0 / (ss * st);

                for (int j = 0; j < nt; j++)
                {
                    double t = (j + 0.5) / nt;
                    for (int i = 0; i < ns; i++)
                    {
                        double s = (i + 0.5) / ns;
                        SampleTexture(texture, Bilerp(uv, s, t), tex);
                        double weight = tex[3] * sampleWeight;
                        if (weight <= 0)
                        {
                            continue;
                        }
                        var sample = new Sample { T0 = tex[0], T1 = tex[1], T2 = tex[2], Weight = weight };
                        LightmapWeights(lx + lw * s - group.X, ly + lh * t - group.Y, group.W, group.H, ref sample);
                        samples.Add(sample);
                    }
                }
            }
            return samples.ToArray();
        }

        static double Filter(double[] values, in Sample s)
        {
            return s.W0 * values[s.I0] + s.W1 * values[s.I1] + s.W2 * values[s.I2] + s.W3 * values[s.I3];
        }

        static double Trace(double[] m, int n, int offset)
        {
            double trace = 0;
            for (int k = 0; k < n; k++)
            {
                trace += m[(offset + k) * n + offset + k];
            }
            return trace;
        }

        // sum(weight * (t * lightmap(x) - target)^2) = rr - 2 x.b + x.M.x
        static double SquaredError(double[] M, double[] B, double rr, double[] x, int n)
        {
            double e = rr;
            for (int i = 0; i < n; i++)
            {
                double row = 0;
                for (int j = 0; j < n; j++)
                {
                    row += M[i * n + j] * x[j];
                }
                e += x[i] * (row - 2.0 * B[i]);
            }
            return Math.Max(e, 0.0);
        }

        static double Rms(double squaredError, double weight)
        {
            // Three channels per sample
            return weight > 0 ? Math.Sqrt(squaredError / (weight * 3.0)) * 255.0 : 0.0;
        }

        // Bilinear lightmap filter in rect-local pixels, clamped to the rect
        static void LightmapWeights(double px, double py, int width, int height, ref Sample s)
        {
            double fx = px - 0.5;
            double fy = py - 0.5;
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            double ax = fx - x0;
            double ay = fy - y0;
            int xa = Math.Clamp(x0, 0, width - 1);
            int xb = Math.Clamp(x0 + 1, 0, width - 1);
            int ya = Math.Clamp(y0, 0, height - 1);
            int yb = Math.Clamp(y0 + 1, 0, height - 1);
            s.I0 = ya * width + xa; s.W0 = (1 - ax) * (1 - ay);
            s.I1 = ya * width + xb; s.W1 = ax * (1 - ay);
            s.I2 = yb * width + xa; s.W2 = (1 - ax) * ay;
            s.I3 = yb * width + xb; s.W3 = ax * ay;
        }

        // Bilinear texture filter with repeat. No texture counts as white.
        static void SampleTexture(FloatImage? texture, Vector2 uv, Span<double> output)
        {
            if (texture == null)
            {
                output[0] = output[1] = output[2] = output[3] = 1.0;
                return;
            }
            double fx = uv.X * (double)texture.Width - 0.5;
            double fy = uv.Y * (double)texture.Height - 0.5;
            double flx = Math.Floor(fx);
            double fly = Math.Floor(fy);
            double ax = fx - flx;
            double ay = fy - fly;
            int xa = Wrap((long)flx, texture.Width);
            int xb = Wrap((long)flx + 1, texture.Width);
            int ya = Wrap((long)fly, texture.Height);
            int yb = Wrap((long)fly + 1, texture.Height);
            int i00 = (ya * texture.Width + xa) * 4;
            int i10 = (ya * texture.Width + xb) * 4;
            int i01 = (yb * texture.Width + xa) * 4;
            int i11 = (yb * texture.Width + xb) * 4;
            var d = texture.Data;
            for (int c = 0; c < 4; c++)
            {
                double top = d[i00 + c] + (d[i10 + c] - d[i00 + c]) * ax;
                double bottom = d[i01 + c] + (d[i11 + c] - d[i01 + c]) * ax;
                output[c] = top + (bottom - top) * ay;
            }
        }

        static int Wrap(long value, int size)
        {
            long m = value % size;
            return (int)(m < 0 ? m + size : m);
        }

        static Vector2[] UVCorners(PatchesJsonHandler.PatchJson patch)
        {
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                uv[i] = new Vector2(patch.UVPoints[i, 0], patch.UVPoints[i, 1]);
            }
            return uv;
        }

        // Same as Ice Saw's UVInterpolate
        static Vector2 Bilerp(Vector2[] uv, double s, double t)
        {
            var a = Vector2.Lerp(uv[0], uv[2], (float)s);
            var b = Vector2.Lerp(uv[1], uv[3], (float)s);
            return Vector2.Lerp(a, b, (float)t);
        }

        // Enough samples per lightmap texel to see every texture pixel under it along one axis
        static int SampleCount(FloatImage? texture, Vector2 startA, Vector2 endA, Vector2 startB, Vector2 endB, int texels)
        {
            if (texture == null)
            {
                return MinSamplesPerTexel;
            }
            var size = new Vector2(texture.Width, texture.Height);
            double span = Math.Max(((endA - startA) * size).Length(), ((endB - startB) * size).Length());
            return Math.Clamp((int)Math.Ceiling(span / texels), MinSamplesPerTexel, MaxSamplesPerTexel);
        }

        static bool CholeskySolve(double[] m, double[] b, int n, double[] x)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = m[j * n + j];
                for (int k = 0; k < j; k++)
                {
                    sum -= m[j * n + k] * m[j * n + k];
                }
                if (sum <= 0)
                {
                    return false;
                }
                double d = Math.Sqrt(sum);
                m[j * n + j] = d;
                for (int i = j + 1; i < n; i++)
                {
                    double s = m[i * n + j];
                    for (int k = 0; k < j; k++)
                    {
                        s -= m[i * n + k] * m[j * n + k];
                    }
                    m[i * n + j] = s / d;
                }
            }
            for (int i = 0; i < n; i++)
            {
                double s = b[i];
                for (int k = 0; k < i; k++)
                {
                    s -= m[i * n + k] * x[k];
                }
                x[i] = s / m[i * n + i];
            }
            for (int i = n - 1; i >= 0; i--)
            {
                double s = x[i];
                for (int k = i + 1; k < n; k++)
                {
                    s -= m[k * n + i] * x[k];
                }
                x[i] = s / m[i * n + i];
            }
            return true;
        }

        static void WriteDebug(string path, List<RectGroup> groups, PatchesJsonHandler Patches, List<FloatImage> ps2Pages, List<FloatImage> standardPages, Dictionary<string, FloatImage?> textures)
        {
            Directory.CreateDirectory(path);
            int scale = Math.Max(1, DebugScale);
            Span<double> tex = stackalloc double[4];
            for (int p = 0; p < ps2Pages.Count; p++)
            {
                var ps2 = ps2Pages[p];
                var std = standardPages[p];
                var projection = new FloatImage(ps2.Width * scale, ps2.Height * scale);
                var ps2Render = new FloatImage(ps2.Width * scale, ps2.Height * scale);
                var stdRender = new FloatImage(ps2.Width * scale, ps2.Height * scale);

                foreach (var group in groups)
                {
                    if (group.Page != p)
                    {
                        continue;
                    }
                    var patch = Patches.Patches[group.Patches[0]];
                    textures.TryGetValue(patch.TexturePath ?? "", out var texture);
                    var uv = UVCorners(patch);
                    double lx = patch.LightMapPoint[0] * ps2.Width;
                    double ly = patch.LightMapPoint[1] * ps2.Height;
                    double lw = patch.LightMapPoint[2] * ps2.Width;
                    double lh = patch.LightMapPoint[3] * ps2.Height;

                    for (int y = group.Y * scale; y < (group.Y + group.H) * scale; y++)
                    {
                        for (int x = group.X * scale; x < (group.X + group.W) * scale; x++)
                        {
                            double px = (x + 0.5) / scale;
                            double py = (y + 0.5) / scale;
                            SampleTexture(texture, Bilerp(uv, (px - lx) / lw, (py - ly) / lh), tex);
                            var sample = new Sample();
                            LightmapWeights(px - group.X, py - group.Y, group.W, group.H, ref sample);

                            double af = PageFilter(ps2, group, sample, 3);
                            int d = (y * projection.Width + x) * 4;
                            for (int c = 0; c < 3; c++)
                            {
                                projection.Data[d + c] = (float)tex[c];
                                ps2Render.Data[d + c] = (float)Math.Clamp((tex[c] - PageFilter(ps2, group, sample, c)) * af, 0.0, 1.0);
                                stdRender.Data[d + c] = (float)Math.Clamp(tex[c] * PageFilter(std, group, sample, c), 0.0, 1.0);
                            }
                            projection.Data[d + 3] = ps2Render.Data[d + 3] = stdRender.Data[d + 3] = 1f;
                        }
                    }
                }

                string name = p.ToString().PadLeft(4, '0');
                projection.Save(Path.Combine(path, name + "_texture.png"));
                ps2Render.Save(Path.Combine(path, name + "_ps2.png"));
                stdRender.Save(Path.Combine(path, name + "_standard.png"));
            }
        }

        static double PageFilter(FloatImage page, RectGroup group, in Sample s, int channel)
        {
            double PageValue(int k) => page.Data[((group.Y + k / group.W) * page.Width + group.X + k % group.W) * 4 + channel];
            return s.W0 * PageValue(s.I0) + s.W1 * PageValue(s.I1) + s.W2 * PageValue(s.I2) + s.W3 * PageValue(s.I3);
        }

        static string PagePath(string folder, int page)
        {
            return Path.Combine(folder, page.ToString().PadLeft(4, '0') + ".png");
        }

        class RectGroup
        {
            public int Page, X, Y, W, H;
            public List<int> Patches = new List<int>();
        }

        struct GroupResult
        {
            public double[][] Texels; // r, g, b, a per rect texel
            public double FitError;
            public double BaseError;
            public double Weight;
        }

        // One texture sample under a patch: its colour, its weight, and the 4 rect texels the lightmap filter reads
        struct Sample
        {
            public int I0, I1, I2, I3;
            public double W0, W1, W2, W3;
            public double T0, T1, T2;
            public double Weight;

            public readonly double T(int c)
            {
                return c == 0 ? T0 : c == 1 ? T1 : T2;
            }

            public readonly void Unpack(Span<int> idx, Span<double> w)
            {
                idx[0] = I0; idx[1] = I1; idx[2] = I2; idx[3] = I3;
                w[0] = W0; w[1] = W1; w[2] = W2; w[3] = W3;
            }
        }

        class FloatImage
        {
            public int Width;
            public int Height;
            public float[] Data; // RGBA 0-1

            public FloatImage(int width, int height)
            {
                Width = width;
                Height = height;
                Data = new float[width * height * 4];
            }

            public static FloatImage Load(string path)
            {
                using var image = Image.Load<Rgba32>(path);
                var pixels = new Rgba32[image.Width * image.Height];
                image.CopyPixelDataTo(pixels);
                var result = new FloatImage(image.Width, image.Height);
                for (int i = 0; i < pixels.Length; i++)
                {
                    result.Data[i * 4] = pixels[i].R / 255f;
                    result.Data[i * 4 + 1] = pixels[i].G / 255f;
                    result.Data[i * 4 + 2] = pixels[i].B / 255f;
                    result.Data[i * 4 + 3] = pixels[i].A / 255f;
                }
                return result;
            }

            public void Save(string path)
            {
                var pixels = new Rgba32[Width * Height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Rgba32(
                        (byte)Math.Clamp((int)Math.Round(Data[i * 4] * 255f), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(Data[i * 4 + 1] * 255f), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(Data[i * 4 + 2] * 255f), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(Data[i * 4 + 3] * 255f), 0, 255));
                }
                using var image = Image.LoadPixelData<Rgba32>(pixels, Width, Height);
                image.SaveAsPng(path);
            }
        }
    }
}
