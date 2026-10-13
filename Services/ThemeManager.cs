using System;
using System.Windows;
using System.Windows.Media;

namespace LucidTune.Services
{
    public static class ThemeManager
    {
        public const string DarkTheme = "Dark";
        public const string LightTheme = "Light";

        public static string CurrentTheme { get; private set; } = DarkTheme;
        public static bool IsDark => CurrentTheme == DarkTheme;

        public static event Action<string>? ThemeChanged;

        public static void ApplyTheme(string? theme)
        {
            if (string.Equals(theme, LightTheme, StringComparison.OrdinalIgnoreCase))
            {
                ApplyLightPalette();
                CurrentTheme = LightTheme;
            }
            else
            {
                ApplyDarkPalette();
                CurrentTheme = DarkTheme;
            }

            ThemeChanged?.Invoke(CurrentTheme);
        }

        private static void SetBrush(string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }

        private static void SetColor(string key, Color color)
        {
            Application.Current.Resources[key] = color;
        }

        private static void ApplyDarkPalette()
        {
            // Widget Brushes
            SetBrush("WidgetBackgroundBrush", Color.FromArgb(0xD9, 0x11, 0x11, 0x14));
            SetBrush("WidgetBorderBrush", Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            SetColor("WidgetDropShadowColor", Color.FromRgb(0x00, 0x00, 0x00));
            SetBrush("WidgetTitleBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("WidgetArtistBrush", Color.FromRgb(0x8E, 0x8E, 0x93));
            SetBrush("WidgetNavButtonBrush", Color.FromRgb(0xA0, 0xA0, 0xAB));
            SetBrush("WidgetNavHoverBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("WidgetNavHoverCircleBrush", Color.FromRgb(0x2A, 0x2A, 0x32));
            SetBrush("WidgetPlayButtonCircleBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("WidgetPlayButtonIconBrush", Color.FromRgb(0x12, 0x12, 0x14));
            SetBrush("WidgetTrackBackgroundBrush", Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
            SetBrush("WidgetVolumeIconBrush", Color.FromRgb(0x8E, 0x8E, 0x98));
            SetBrush("WidgetHideButtonBrush", Color.FromRgb(0x70, 0x70, 0x78));
            SetBrush("WidgetHideButtonHoverBgBrush", Color.FromRgb(0x2D, 0x2D, 0x38));

            // Settings Window Brushes
            SetBrush("SettingsWindowBackgroundBrush", Color.FromRgb(0x14, 0x14, 0x1A));
            SetBrush("SettingsWindowBorderBrush", Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            SetBrush("SettingsHeaderBackgroundBrush", Color.FromRgb(0x19, 0x19, 0x22));
            SetBrush("SettingsCardBackgroundBrush", Color.FromRgb(0x19, 0x19, 0x22));
            SetBrush("SettingsSubCardBackgroundBrush", Color.FromRgb(0x14, 0x14, 0x1C));
            SetBrush("SettingsBorderBrush", Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            SetBrush("SettingsSubCardBorderBrush", Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            SetBrush("SettingsTextPrimaryBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("SettingsTextSecondaryBrush", Color.FromRgb(0x8E, 0x8E, 0x98));
            SetBrush("SettingsTextMutedBrush", Color.FromRgb(0x6C, 0x6C, 0x78));
            SetBrush("SettingsInputBackgroundBrush", Color.FromRgb(0x1C, 0x1C, 0x26));
            SetBrush("SettingsInputBorderBrush", Color.FromRgb(0x28, 0x28, 0x38));
            SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x2A, 0x2A, 0x38));
            SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x4E, 0x75, 0xFF));
            SetBrush("SettingsButtonSecondaryBackgroundBrush", Color.FromRgb(0x28, 0x28, 0x36));
            SetBrush("SettingsButtonSecondaryTextBrush", Color.FromRgb(0xE0, 0xE0, 0xE6));
            SetBrush("SettingsScrollBarThumbBrush", Color.FromRgb(0x3A, 0x3A, 0x4D));
            SetBrush("SettingsSliderTrackBrush", Color.FromRgb(0x26, 0x26, 0x34));
            SetBrush("SettingsSeparatorBrush", Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
        }

        private static void ApplyLightPalette()
        {
            // Widget Brushes
            SetBrush("WidgetBackgroundBrush", Color.FromArgb(0xEE, 0xF8, 0xF9, 0xFA));
            SetBrush("WidgetBorderBrush", Color.FromArgb(0x2E, 0x00, 0x00, 0x00));
            SetColor("WidgetDropShadowColor", Color.FromArgb(0x40, 0x00, 0x00, 0x00));
            SetBrush("WidgetTitleBrush", Color.FromRgb(0x11, 0x18, 0x27));
            SetBrush("WidgetArtistBrush", Color.FromRgb(0x4B, 0x55, 0x63));
            SetBrush("WidgetNavButtonBrush", Color.FromRgb(0x4B, 0x55, 0x63));
            SetBrush("WidgetNavHoverBrush", Color.FromRgb(0x11, 0x18, 0x27));
            SetBrush("WidgetNavHoverCircleBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("WidgetPlayButtonCircleBrush", Color.FromRgb(0x11, 0x18, 0x27));
            SetBrush("WidgetPlayButtonIconBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("WidgetTrackBackgroundBrush", Color.FromArgb(0x24, 0x00, 0x00, 0x00));
            SetBrush("WidgetVolumeIconBrush", Color.FromRgb(0x4B, 0x55, 0x63));
            SetBrush("WidgetHideButtonBrush", Color.FromRgb(0x6B, 0x72, 0x80));
            SetBrush("WidgetHideButtonHoverBgBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));

            // Settings Window Brushes
            SetBrush("SettingsWindowBackgroundBrush", Color.FromRgb(0xF3, 0xF4, 0xF6));
            SetBrush("SettingsWindowBorderBrush", Color.FromArgb(0x30, 0x00, 0x00, 0x00));
            SetBrush("SettingsHeaderBackgroundBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("SettingsCardBackgroundBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("SettingsSubCardBackgroundBrush", Color.FromRgb(0xF8, 0xF9, 0xFA));
            SetBrush("SettingsBorderBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsSubCardBorderBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsTextPrimaryBrush", Color.FromRgb(0x11, 0x18, 0x27));
            SetBrush("SettingsTextSecondaryBrush", Color.FromRgb(0x4B, 0x55, 0x63));
            SetBrush("SettingsTextMutedBrush", Color.FromRgb(0x9C, 0xA3, 0xAF));
            SetBrush("SettingsInputBackgroundBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
            SetBrush("SettingsInputBorderBrush", Color.FromRgb(0xD1, 0xD5, 0xDB));
            SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xEE, 0xF2, 0xFF));
            SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x3B, 0x82, 0xF6));
            SetBrush("SettingsButtonSecondaryBackgroundBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsButtonSecondaryTextBrush", Color.FromRgb(0x1F, 0x29, 0x37));
            SetBrush("SettingsScrollBarThumbBrush", Color.FromRgb(0xD1, 0xD5, 0xDB));
            SetBrush("SettingsSliderTrackBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsSeparatorBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
        }
    }
}
