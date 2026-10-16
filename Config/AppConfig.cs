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

        // Appearance
        public double WidgetOpacity { get; set; } = 1.0;
        public bool CompactMode { get; set; } = false;
        public bool ShowVolumeControl { get; set; } = true;
        public string Theme { get; set; } = "Dark";
        public string AccentColor { get; set; } = "Yellow";

        // Discord Rich Presence
        public bool DiscordRpcEnabled { get; set; } = true;
        public string DiscordApplicationId { get; set; } = "1560410469112750100";
        public string? DiscordYandexMusicAppId { get; set; }
        public string? DiscordSpotifyAppId { get; set; }
        public string? DiscordSoundCloudAppId { get; set; }
    }
}
