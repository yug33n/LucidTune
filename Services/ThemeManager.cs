using System;
using System.Windows;
using System.Windows.Media;

namespace LucidTune.Services
{
    public static class ThemeManager
    {
        public const string DarkTheme = "Dark";
        public const string LightTheme = "Light";

        public const string AccentYellow = "Yellow";
        public const string AccentBlue = "Blue";
        public const string AccentPurple = "Purple";
        public const string AccentGreen = "Green";
        public const string AccentOrange = "Orange";
        public const string AccentRed = "Red";
        public const string AccentPink = "Pink";

        public sealed class AccentOption
        {
            public string Key { get; }
            public string Name { get; }
            public SolidColorBrush ColorBrush { get; }

            public AccentOption(string key, string name, Color color)
            {
                Key = key;
                Name = name;
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                ColorBrush = brush;
            }
        }

        public static readonly System.Collections.Generic.IReadOnlyList<AccentOption> AvailableAccents = new System.Collections.Generic.List<AccentOption>
        {
            new(AccentYellow, "Жёлтый", Color.FromRgb(0xFF, 0xB8, 0x00)),
            new(AccentBlue, "Синий", Color.FromRgb(0x4E, 0x75, 0xFF)),
            new(AccentPurple, "Фиолетовый", Color.FromRgb(0x8B, 0x5C, 0xF6)),
            new(AccentGreen, "Зелёный", Color.FromRgb(0x10, 0xB9, 0x81)),
            new(AccentOrange, "Оранжевый", Color.FromRgb(0xF9, 0x73, 0x16)),
            new(AccentRed, "Красный", Color.FromRgb(0xEF, 0x44, 0x44)),
            new(AccentPink, "Розовый", Color.FromRgb(0xEC, 0x48, 0x99)),
        }.AsReadOnly();

        public static string CurrentTheme { get; private set; } = DarkTheme;
        public static string CurrentAccent { get; private set; } = AccentYellow;
        public static bool IsDark => CurrentTheme == DarkTheme;
        public static bool IsDynamicAccentActive { get; private set; }
        public static Color? CurrentDynamicAccentColor { get; private set; }

        public static event Action<string, string>? ThemeChanged;

        public static void ApplyTheme(string? theme, string? accent = null)
        {
            IsDynamicAccentActive = false;
            CurrentDynamicAccentColor = null;

            if (!string.IsNullOrWhiteSpace(theme))
            {
                CurrentTheme = string.Equals(theme, LightTheme, StringComparison.OrdinalIgnoreCase)
                    ? LightTheme
                    : DarkTheme;
            }

            if (!string.IsNullOrWhiteSpace(accent))
            {
                CurrentAccent = NormalizeAccent(accent);
            }

            if (IsDark)
            {
                ApplyDarkPalette();
            }
            else
            {
                ApplyLightPalette();
            }

            ApplyAccentPalette(CurrentAccent, IsDark);

            ThemeChanged?.Invoke(CurrentTheme, CurrentAccent);
        }

        public static void ApplyDynamicAccent(Color accentColor)
        {
            CurrentDynamicAccentColor = accentColor;
            IsDynamicAccentActive = true;

            SetBrush("AccentBrush", accentColor);

            var hoverColor = Color.FromRgb(
                (byte)Math.Min(255, accentColor.R + 25),
                (byte)Math.Min(255, accentColor.G + 25),
                (byte)Math.Min(255, accentColor.B + 25)
            );
            SetBrush("AccentHoverBrush", hoverColor);

            double luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
            Color textColor = luminance > 0.65 ? Color.FromRgb(0x14, 0x14, 0x16) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            SetBrush("AccentTextBrush", textColor);

            if (IsDark)
            {
                SetBrush("WidgetBorderBrush", Color.FromArgb(0x45, accentColor.R, accentColor.G, accentColor.B));
                SetBrush("SettingsBadgeBackgroundBrush", Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));
                SetBrush("SettingsBadgeTextBrush", hoverColor);
            }
            else
            {
                SetBrush("WidgetBorderBrush", Color.FromArgb(0x35, accentColor.R, accentColor.G, accentColor.B));
                SetBrush("SettingsBadgeBackgroundBrush", Color.FromArgb(0x30, accentColor.R, accentColor.G, accentColor.B));
                SetBrush("SettingsBadgeTextBrush", accentColor);
            }

            ThemeChanged?.Invoke(CurrentTheme, "Dynamic");
        }

        public static void RestoreStaticAccent()
        {
            if (!IsDynamicAccentActive) return;

            IsDynamicAccentActive = false;
            CurrentDynamicAccentColor = null;

            if (IsDark)
            {
                SetBrush("WidgetBorderBrush", Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                SetBrush("WidgetBorderBrush", Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            }

            ApplyAccentPalette(CurrentAccent, IsDark);
            ThemeChanged?.Invoke(CurrentTheme, CurrentAccent);
        }

        private static string NormalizeAccent(string accent)
        {
            return accent.ToLowerInvariant() switch
            {
                "yellow" or "жёлтый" or "желтый" => AccentYellow,
                "blue" or "синий" => AccentBlue,
                "purple" or "фиолетовый" => AccentPurple,
                "green" or "зелёный" or "зеленый" => AccentGreen,
                "orange" or "оранжевый" => AccentOrange,
                "red" or "красный" => AccentRed,
                "pink" or "розовый" => AccentPink,
                _ => AccentYellow
            };
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
            SetBrush("SettingsButtonSecondaryBackgroundBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsButtonSecondaryTextBrush", Color.FromRgb(0x1F, 0x29, 0x37));
            SetBrush("SettingsScrollBarThumbBrush", Color.FromRgb(0xD1, 0xD5, 0xDB));
            SetBrush("SettingsSliderTrackBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
            SetBrush("SettingsSeparatorBrush", Color.FromRgb(0xE5, 0xE7, 0xEB));
        }

        private static void ApplyAccentPalette(string accent, bool isDark)
        {
            switch (accent)
            {
                case AccentYellow:
                    SetBrush("AccentBrush", Color.FromRgb(0xFF, 0xB8, 0x00)); // Vivid Amber Yellow
                    SetBrush("AccentHoverBrush", Color.FromRgb(0xFF, 0xC7, 0x2C));
                    SetBrush("AccentTextBrush", Color.FromRgb(0x14, 0x14, 0x16)); // High contrast dark text on yellow
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x36, 0x2C, 0x12));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xFF, 0xC7, 0x2C));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xFE, 0xF3, 0xC7));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xB4, 0x53, 0x09));
                    }
                    break;

                case AccentBlue:
                    SetBrush("AccentBrush", Color.FromRgb(0x4E, 0x75, 0xFF));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0x66, 0x88, 0xFF));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x2A, 0x2A, 0x38));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x6B, 0x8E, 0xFF));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xE0, 0xE7, 0xFF));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x1D, 0x4E, 0xD8));
                    }
                    break;

                case AccentPurple:
                    SetBrush("AccentBrush", Color.FromRgb(0x8B, 0x5C, 0xF6));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0xA7, 0x8B, 0xFA));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x2D, 0x20, 0x3C));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xA7, 0x8B, 0xFA));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xF3, 0xE8, 0xFF));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x6D, 0x28, 0xD9));
                    }
                    break;

                case AccentGreen:
                    SetBrush("AccentBrush", Color.FromRgb(0x10, 0xB9, 0x81));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0x34, 0xD3, 0x99));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x16, 0x2E, 0x23));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x34, 0xD3, 0x99));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xD1, 0xFA, 0xE5));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0x04, 0x78, 0x57));
                    }
                    break;

                case AccentOrange:
                    SetBrush("AccentBrush", Color.FromRgb(0xF9, 0x73, 0x16));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0xFB, 0x92, 0x3C));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x34, 0x20, 0x14));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xFB, 0x92, 0x3C));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xFF, 0xED, 0xD5));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xC2, 0x41, 0x0C));
                    }
                    break;

                case AccentRed:
                    SetBrush("AccentBrush", Color.FromRgb(0xEF, 0x44, 0x44));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0xF8, 0x71, 0x71));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x36, 0x1A, 0x20));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xF8, 0x71, 0x71));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xFE, 0xE2, 0xE2));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xB9, 0x1C, 0x1C));
                    }
                    break;

                case AccentPink:
                    SetBrush("AccentBrush", Color.FromRgb(0xEC, 0x48, 0x99));
                    SetBrush("AccentHoverBrush", Color.FromRgb(0xF4, 0x72, 0xB6));
                    SetBrush("AccentTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF));
                    if (isDark)
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0x36, 0x1A, 0x2B));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xF4, 0x72, 0xB6));
                    }
                    else
                    {
                        SetBrush("SettingsBadgeBackgroundBrush", Color.FromRgb(0xFC, 0xE7, 0xF3));
                        SetBrush("SettingsBadgeTextBrush", Color.FromRgb(0xBE, 0x18, 0x5D));
                    }
                    break;

                default:
                    ApplyAccentPalette(AccentYellow, isDark);
                    break;
            }
        }
    }
}
