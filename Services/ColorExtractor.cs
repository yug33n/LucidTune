using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LucidTune.Services
{
    public static class ColorExtractor
    {
        private const int SampleSize = 36;
        private const int HueBucketCount = 18; // 20 degrees each

        private sealed class HueBucket
        {
            public double TotalWeight { get; set; }
            public double TotalR { get; set; }
            public double TotalG { get; set; }
            public double TotalB { get; set; }
            public int PixelCount { get; set; }
        }

        public static Color? ExtractDominantAccentColor(BitmapSource? source)
        {
            if (source == null) return null;

            try
            {
                // Scale down to 36x36 for instant processing (<1ms)
                var scaled = new TransformedBitmap(source, new ScaleTransform(
                    (double)SampleSize / Math.Max(1, source.PixelWidth),
                    (double)SampleSize / Math.Max(1, source.PixelHeight)
                ));

                var formatted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
                formatted.Freeze();

                int width = formatted.PixelWidth;
                int height = formatted.PixelHeight;
                int stride = width * 4;
                var pixels = new byte[height * stride];
                formatted.CopyPixels(pixels, stride, 0);

                var buckets = new HueBucket[HueBucketCount];
                for (int i = 0; i < HueBucketCount; i++)
                {
                    buckets[i] = new HueBucket();
                }

                int qualifiedPixels = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a < 128) continue;

                    RgbToHsl(r, g, b, out double h, out double s, out double l);

                    // Skip near-black, near-white, or dull grays
                    if (l < 0.16 || l > 0.88 || s < 0.22)
                    {
                        continue;
                    }

                    // Score saturation and balanced lightness
                    double lightnessScore = 1.0 - Math.Abs(l - 0.55) * 1.4;
                    if (lightnessScore <= 0) lightnessScore = 0.05;

                    double weight = Math.Pow(s, 1.25) * lightnessScore;

                    int bucketIndex = Math.Clamp((int)(h / (360.0 / HueBucketCount)), 0, HueBucketCount - 1);
                    var bucket = buckets[bucketIndex];
                    bucket.TotalWeight += weight;
                    bucket.TotalR += r * weight;
                    bucket.TotalG += g * weight;
                    bucket.TotalB += b * weight;
                    bucket.PixelCount++;
                    qualifiedPixels++;
                }

                if (qualifiedPixels < 6)
                {
                    // Too few colorful pixels (mostly monochrome or black/white)
                    return null;
                }

                // Find bucket with highest weight
                HueBucket? bestBucket = null;
                double maxWeight = -1;
                foreach (var b in buckets)
                {
                    if (b.TotalWeight > maxWeight)
                    {
                        maxWeight = b.TotalWeight;
                        bestBucket = b;
                    }
                }

                if (bestBucket == null || bestBucket.TotalWeight <= 0)
                {
                    return null;
                }

                byte avgR = (byte)Math.Clamp((int)(bestBucket.TotalR / bestBucket.TotalWeight), 0, 255);
                byte avgG = (byte)Math.Clamp((int)(bestBucket.TotalG / bestBucket.TotalWeight), 0, 255);
                byte avgB = (byte)Math.Clamp((int)(bestBucket.TotalB / bestBucket.TotalWeight), 0, 255);

                // Polish color for UI elegance (boost saturation and clamp lightness)
                RgbToHsl(avgR, avgG, avgB, out double resH, out double resS, out double resL);

                // Boost saturation if slightly muted
                if (resS < 0.65) resS = Math.Min(1.0, resS * 1.35);

                // Clamp lightness so accent pops well on dark cards
                resL = Math.Clamp(resL, 0.44, 0.62);

                HslToRgb(resH, resS, resL, out byte finalR, out byte finalG, out byte finalB);

                return Color.FromRgb(finalR, finalG, finalB);
            }
            catch
            {
                return null;
            }
        }

        private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;

            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            l = (max + min) / 2.0;

            if (Math.Abs(delta) < 0.0001)
            {
                h = 0;
                s = 0;
                return;
            }

            s = l <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

            if (Math.Abs(max - rf) < 0.0001)
            {
                h = ((gf - bf) / delta) + (gf < bf ? 6.0 : 0.0);
            }
            else if (Math.Abs(max - gf) < 0.0001)
            {
                h = ((bf - rf) / delta) + 2.0;
            }
            else
            {
                h = ((rf - gf) / delta) + 4.0;
            }

            h *= 60.0;
        }

        private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
        {
            if (s <= 0.0001)
            {
                byte val = (byte)Math.Clamp((int)(l * 255.0), 0, 255);
                r = val;
                g = val;
                b = val;
                return;
            }

            double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
            double p = 2.0 * l - q;
            double hk = h / 360.0;

            r = (byte)Math.Clamp((int)(HueToRgb(p, q, hk + 1.0 / 3.0) * 255.0), 0, 255);
            g = (byte)Math.Clamp((int)(HueToRgb(p, q, hk) * 255.0), 0, 255);
            b = (byte)Math.Clamp((int)(HueToRgb(p, q, hk - 1.0 / 3.0) * 255.0), 0, 255);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0.0) t += 1.0;
            if (t > 1.0) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }
    }
}
