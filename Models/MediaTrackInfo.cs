using System;
using System.Windows.Media.Imaging;

namespace LucidTune.Models
{
    public class MediaTrackInfo
    {
        public string Title { get; set; } = "Нет активного трека";
        public string Artist { get; set; } = "Запустите музыку";
        public BitmapImage? Thumbnail { get; set; }
        public byte[]? ThumbnailBytes { get; set; }
        public bool IsPlaying { get; set; }
        public bool HasActiveSession { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
        public string? SourceAppUserModelId { get; set; }

        public double Progress
        {
            get
            {
                if (Duration <= TimeSpan.Zero) return 0.0;
                var progress = Position.TotalMilliseconds / Duration.TotalMilliseconds;
                return Math.Clamp(progress, 0.0, 1.0);
            }
        }
    }
}
