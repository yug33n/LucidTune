using System.Text.Json.Serialization;

namespace LucidTune.Config
{
    public class AppConfig
    {
        public double? WindowX { get; set; }
        public double? WindowY { get; set; }
        public string HotkeyModifiers { get; set; } = "Alt";
        public string HotkeyKey { get; set; } = "M";
        public bool AutoHideInFullscreen { get; set; } = false;
        public bool AutoStart { get; set; } = false;
        public bool ShowSettingsOnStartup { get; set; } = true;
    }
}
