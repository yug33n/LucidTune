using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LucidTune.Services
{
    public sealed class ExtractedPalette
    {
        public Color PrimaryAccent { get; set; }
        public Color SecondaryAccent { get; set; }
        public Color AmbientShimmer1 { get; set; }
        public Color AmbientShimmer2 { get; set; }
        public Color AmbientShimmer3 { get; set; }
    }

    public static class ColorExtractor
    {
        private const int HueBucketCount = 24; // 15 degrees per bucket

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
            return ExtractPalette(source)?.PrimaryAccent;
        }

        public static ExtractedPalette? ExtractPalette(BitmapSource? source)
        {
            if (source == null) return null;

            try
            {
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                if (width <= 0 || height <= 0) return null;

                // Format conversion to Bgra32 if necessary
                BitmapSource bgraSource;
                if (source.Format == PixelFormats.Bgra32)
                {
                    bgraSource = source;
                }
                else
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = PixelFormats.Bgra32;
                    converted.EndInit();
                    converted.Freeze();
                    bgraSource = converted;
                }

                // Sample points across image (step size to inspect up to ~40x40 grid)
                int sampleCount = 40;
                int stepX = Math.Max(1, width / sampleCount);
                int stepY = Math.Max(1, height / sampleCount);

                int stride = width * 4;
                var rawPixels = new byte[height * stride];
                bgraSource.CopyPixels(rawPixels, stride, 0);

                var buckets = new HueBucket[HueBucketCount];
                for (int i = 0; i < HueBucketCount; i++)
                {
                    buckets[i] = new HueBucket();
                }

                double fallbackTotalR = 0;
                double fallbackTotalG = 0;
                double fallbackTotalB = 0;
                int fallbackCount = 0;

                int qualifiedPixels = 0;

                for (int y = 0; y < height; y += stepY)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x += stepX)
                    {
                        int index = rowOffset + x * 4;
                        if (index + 3 >= rawPixels.Length) continue;

                        byte b = rawPixels[index];
                        byte g = rawPixels[index + 1];
                        byte r = rawPixels[index + 2];
                        byte a = rawPixels[index + 3];

                        if (a < 128) continue;

                        RgbToHsl(r, g, b, out double h, out double s, out double l);

                        // Accumulate overall non-black fallback
                        if (l > 0.08 && l < 0.96)
                        {
                            fallbackTotalR += r;
                            fallbackTotalG += g;
                            fallbackTotalB += b;
                            fallbackCount++;
                        }

                        // Skip near-black, near-white, or dull grays for vibrant pass
                        if (l < 0.12 || l > 0.90 || s < 0.14)
                        {
                            continue;
                        }

                        // Score saturation and balanced lightness
                        double lightnessScore = 1.0 - Math.Abs(l - 0.52) * 1.3;
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
                }

                // Primary and secondary buckets
                HueBucket? bestBucket = null;
                HueBucket? secondBucket = null;
                int bestBucketIndex = -1;
                double maxWeight = -1;

                for (int i = 0; i < HueBucketCount; i++)
                {
                    var b = buckets[i];
                    if (b.TotalWeight > maxWeight)
                    {
                        maxWeight = b.TotalWeight;
                        bestBucket = b;
                        bestBucketIndex = i;
                    }
                }

                Color primaryColor;
                Color secondaryColor;

                if (bestBucket == null || qualifiedPixels < 4 || bestBucket.TotalWeight <= 0)
                {
                    if (fallbackCount > 0)
                    {
                        byte avgR = (byte)Math.Clamp((int)(fallbackTotalR / fallbackCount), 0, 255);
                        byte avgG = (byte)Math.Clamp((int)(fallbackTotalG / fallbackCount), 0, 255);
                        byte avgB = (byte)Math.Clamp((int)(fallbackTotalB / fallbackCount), 0, 255);

                        RgbToHsl(avgR, avgG, avgB, out double fH, out double fS, out double fL);
                        // Boost saturation slightly so it's not a dead gray
                        fS = Math.Clamp(fS * 1.5 + 0.25, 0.35, 0.70);
                        fL = Math.Clamp(fL, 0.44, 0.60);

                        HslToRgb(fH, fS, fL, out byte fR, out byte fG, out byte fB);
                        primaryColor = Color.FromRgb(fR, fG, fB);

                        HslToRgb((fH + 35.0) % 360.0, fS * 0.85, fL, out byte sR, out byte sG, out byte sB);
                        secondaryColor = Color.FromRgb(sR, sG, sB);
                    }
                    else
                    {
                        // Default elegant violet/cyan fallback
                        primaryColor = Color.FromRgb(0x4E, 0x75, 0xFF);
                        secondaryColor = Color.FromRgb(0x8B, 0x5C, 0xF6);
                    }
                }
                else
                {
                    byte avgR = (byte)Math.Clamp((int)(bestBucket.TotalR / bestBucket.TotalWeight), 0, 255);
                    byte avgG = (byte)Math.Clamp((int)(bestBucket.TotalG / bestBucket.TotalWeight), 0, 255);
                    byte avgB = (byte)Math.Clamp((int)(bestBucket.TotalB / bestBucket.TotalWeight), 0, 255);

                    RgbToHsl(avgR, avgG, avgB, out double resH, out double resS, out double resL);
                    // Boost saturation if slightly muted
                    if (resS < 0.65) resS = Math.Min(1.0, resS * 1.35);
                    // Clamp lightness so accent pops well on dark cards
                    resL = Math.Clamp(resL, 0.44, 0.62);
                    HslToRgb(resH, resS, resL, out byte finalR, out byte finalG, out byte finalB);
                    primaryColor = Color.FromRgb(finalR, finalG, finalB);

                    // Find secondary distinct bucket (at least 3 buckets / 45 degrees away)
                    double secondMaxWeight = -1;
                    for (int i = 0; i < HueBucketCount; i++)
                    {
                        int diff = Math.Abs(i - bestBucketIndex);
                        if (diff > HueBucketCount / 2) diff = HueBucketCount - diff;
                        if (diff < 3) continue; // Skip buckets too close in hue

                        if (buckets[i].TotalWeight > secondMaxWeight)
                        {
                            secondMaxWeight = buckets[i].TotalWeight;
                            secondBucket = buckets[i];
                        }
                    }

                    if (secondBucket != null && secondBucket.TotalWeight > 0.1 * bestBucket.TotalWeight)
                    {
                        byte sR = (byte)Math.Clamp((int)(secondBucket.TotalR / secondBucket.TotalWeight), 0, 255);
                        byte sG = (byte)Math.Clamp((int)(secondBucket.TotalG / secondBucket.TotalWeight), 0, 255);
                        byte sB = (byte)Math.Clamp((int)(secondBucket.TotalB / secondBucket.TotalWeight), 0, 255);
                        RgbToHsl(sR, sG, sB, out double sH, out double sS, out double sL);
                        if (sS < 0.60) sS = Math.Min(1.0, sS * 1.3);
                        sL = Math.Clamp(sL, 0.42, 0.60);
                        HslToRgb(sH, sS, sL, out byte fSR, out byte fSG, out byte fSB);
                        secondaryColor = Color.FromRgb(fSR, fSG, fSB);
                    }
                    else
                    {
                        // Harmonized analogous hue (+35 degrees)
                        HslToRgb((resH + 35.0) % 360.0, resS * 0.9, resL, out byte fSR, out byte fSG, out byte fSB);
                        secondaryColor = Color.FromRgb(fSR, fSG, fSB);
                    }
                }

                return new ExtractedPalette
                {
                    PrimaryAccent = primaryColor,
                    SecondaryAccent = secondaryColor,
                    AmbientShimmer1 = Color.FromArgb(0x40, primaryColor.R, primaryColor.G, primaryColor.B),
                    AmbientShimmer2 = Color.FromArgb(0x28, secondaryColor.R, secondaryColor.G, secondaryColor.B),
                    AmbientShimmer3 = Color.FromArgb(0x18, primaryColor.R, primaryColor.G, primaryColor.B)
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ColorExtractor Exception] {ex.Message}");
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
