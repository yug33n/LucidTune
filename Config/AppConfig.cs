using System.Text.Json.Serialization;

namespace LucidTune.Config
{
    public class AppConfig
    {
        public double? WindowX { get; set; }
        public double? WindowY { get; set; }
        
        // Visibility toggle hotkey
        public string HotkeyModifiers { get; set; } = "Alt";
        public string HotkeyKey { get; set; } = "M";

        // Media controls hotkeys
        public bool MediaHotkeysEnabled { get; set; } = true;
        public string PlayPauseModifiers { get; set; } = "Alt";
        public string PlayPauseKey { get; set; } = "Space";
        public string NextTrackModifiers { get; set; } = "Alt";
        public string NextTrackKey { get; set; } = "Right";
        public string PrevTrackModifiers { get; set; } = "Alt";
        public string PrevTrackKey { get; set; } = "Left";

        // System preferences
        public bool AutoHideInFullscreen { get; set; } = false;
        public bool AutoStart { get; set; } = false;
        public bool ShowSettingsOnStartup { get; set; } = true;

        // Language
        public string Language { get; set; } = "ru";

        // Appearance
        public double WidgetOpacity { get; set; } = 1.0;
        public bool CompactMode { get; set; } = false;
        public bool ShowVolumeControl { get; set; } = true;
        public bool ShowAudioSpectrum { get; set; } = true;
        public bool TaskbarModeEnabled { get; set; } = false;
        public string TaskbarPosition { get; set; } = "Right";
        public double TaskbarOffsetX { get; set; } = 0;
        public string Theme { get; set; } = "Dark";
        public string AccentColor { get; set; } = "Yellow";
        public bool AdaptiveColorEnabled { get; set; } = true;
        public bool AdaptiveBackgroundShimmer { get; set; } = true;

        // Snapping and Capture
        public bool SnapToScreenEdges { get; set; } = true;
        public bool ExcludeFromCapture { get; set; } = false;

        // Discord Rich Presence
        public bool DiscordRpcEnabled { get; set; } = true;
        public bool DiscordIgnoreBrowserVideos { get; set; } = true;
        public bool DiscordIgnoreTelegramVoice { get; set; } = true;
        public string DiscordApplicationId { get; set; } = "1560410469112750100";
        public string? DiscordYandexMusicAppId { get; set; }
        public string? DiscordSpotifyAppId { get; set; }
        public string? DiscordSoundCloudAppId { get; set; }
    }
}
