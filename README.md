# LucidTune 🎵

**English** | [Русский](README_RU.md)

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg?logo=windows)](https://microsoft.com/windows)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0%20WPF-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release: v2.1.0](https://img.shields.io/badge/Release-v2.1.0-4E75FF.svg)](https://github.com/yug33n/LucidTune/releases)

**LucidTune** is an ultra-lightweight, customizable desktop overlay and taskbar media widget for Windows 10 and 11. Designed with a sleek frosted glass aesthetic inspired by Apple's Dynamic Island, LucidTune brings real-time media metadata, album cover artwork, track playback progress, volume adjustment, and responsive playback controls directly above your apps, games, or embedded into the Windows taskbar.

---

## ✨ Features in v2.1.0

### 📌 Windows Taskbar Dock Mini-Player
- **Seamless Taskbar Integration**: Dock playback controls directly into the Windows taskbar without cluttering your desktop.
- **Two Flexible Placement Modes**:
  - **Right (System Tray Dock)**: Compact mini-player with intelligent, dynamic width clamping (54px to 150px) that automatically scales down when taskbar space is tight, guaranteeing it never overlaps running application icons.
  - **Left (Start Menu Dock)**: Expanded presentation displaying album artwork, track title, artist, progress timeline, and controls.
- **Windows 11 Weather / Widgets Avoidance**: Dynamically detects the Windows 11 Weather / Widgets board on the left side of the taskbar via UI Automation and registry signals, cleanly positioning itself with a comfortable margin without overlapping weather information.
- **Mouse Wheel Volume Control**: Scroll your mouse wheel directly over the taskbar widget to smoothly adjust system or app volume.

### 📊 Real Audio-Reactive Spectrum Visualizer
- **Native WASAPI Peak Metering**: Real-time audio amplitude analysis powered by the Windows Core Audio `IAudioMeterInformation` API.
- **Physically-Modeled Equalizer Dynamics**: Fast-attack response on beats and punchy bass drops, with smooth organic decay during melodic sections.
- **Perceptual Loudness Scaling**: Utilizes logarithmic square-root curve mapping so bars dance vividly whether listening at whisper-quiet or high volume levels.
- **Instant Silence Detection**: Automatically flattens bars down to a subtle 2px line during musical breakdowns, pauses, or silence. Can be toggled on or off in Settings.

### 🌐 Bilingual Interface (English & Russian)
- Instant, one-click language switching between **English (EN)** and **Russian (RU)**.
- Every dialog, dropdown, hotkey label, status card, tooltip, and tray context menu updates dynamically on the fly without needing an application restart.

### 🛡️ Discord Rich Presence & Smart External Media Filtering
- **Live Status Profile**: Broadcasts current song title, artist, cover art, and real-time playback timeline to your Discord status.
- **Multi-Engine Cover Resolution**: Automatically resolves cover art across Yandex Music, Deezer/Spotify, Apple Music, and SoundCloud/Genius.
- **Unified External Media Filter**: Easily prevent browser videos (YouTube, Twitch) and Telegram voice notes/video circles from broadcasting as music tracks in Discord.

### 🌈 Adaptive Palette & Ambient Cover Shimmer
- **Automatic Color Extraction**: Dynamically samples dominant and vibrant hues from current album art and applies them in real time across widget accents, progress bar, borders, and badges.
- **Ambient Background Shimmer**: An optional live gradient shimmer that softly breathes across the widget background in the hues of the current album cover.
- **Graceful Fallback**: Automatically reverts to user's chosen static accent theme when cover art is missing or feature is disabled.

### 🧲 Screen Edge & Center Snapping
- **Magnetic Docking**: While dragging the widget across the screen, it gently snaps to monitor boundaries (with an aesthetic floating dock padding) and to the horizontal center.
- Can be disabled in Settings for freeform pixel-level positioning.

### 👻 Streamer Mode (Anti-Screen Capture for OBS & Discord)
- **Invisible to Stream Captures**: Utilizes Win32 `WDA_EXCLUDEFROMCAPTURE` technology so the overlay is visible on your monitor but completely invisible to OBS Studio, Discord Screen Share, and screenshot capture tools.
- Perfect for streamers, gamers, and content creators.

### 🎶 Universal Windows Media (SMTC) Integration
- Seamlessly synchronizes with Windows **System Media Transport Controls**.
- Automatically captures media sessions from **Spotify, YouTube Music / Chrome, Edge, Firefox, Telegram, VLC, Yandex Music, AIMP, Apple Music**, and any other SMTC-compatible media player.
- Live track titles, artist details, dynamic progress bar, and crisp album art with an elegant vector fallback.
- Rapid track switching safety guards (debounce protection preventing null-reference crashes).

### ⚙️ Rich Configuration Window & Hotkeys
- **Center Reset**: One-click button to reset widget position to bottom-center.
- **Global Hotkey Customization**:
  - Show / Hide widget (defaults to `Alt + M`).
  - Play / Pause.
  - Next Track.
  - Previous Track.
  - Full support for `Ctrl`, `Alt`, `Shift`, and `Win` modifiers.
- **Fullscreen & Game Detection**: Optional automatic hiding when launching fullscreen games or applications.
- **System Integration**: Toggle launch on Windows startup and startup window display.
- **Single-Instance Mutex & Crash Logger**: Fast, crash-resilient process management with signal waking and automatic crash logging to `%APPDATA%\LucidTune\crash.log`.

---

## 🖥️ System Requirements

- **OS**: Windows 10 (Build 19041+) or Windows 11 (x64)
- **Runtime**:
  - **Standalone Build (Self-Contained)**: No external dependencies or runtime required.
  - **Portable Build**: [.NET 9.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)

---

## 🚀 Installation & Running

### Option 1. Standalone Single-File Executable (Recommended)
Runs out of the box on any compatible Windows PC without requiring .NET 9 pre-installed:
```powershell
./publish/standalone/LucidTune.exe
```

### Option 2. Portable Framework-Dependent Build
Lightweight build for systems that already have the .NET 9.0 Desktop Runtime installed:
```powershell
./publish/portable/LucidTune.exe
```

---

## 🛠️ Building from Source

Clone the repository:
```powershell
git clone https://github.com/yug33n/LucidTune.git
cd LucidTune
```

### Build Solution
```powershell
dotnet build -c Release
```

### Publish Standalone Single-File Executable
```powershell
dotnet publish LucidTune.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/standalone
```

### Publish Portable Single-File Executable
```powershell
dotnet publish LucidTune.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish/portable
```

---

## ⌨️ Default Hotkeys

| Action | Shortcut | Description |
|---|---|---|
| **Toggle Widget** | `Alt + M` | Quickly hide or show the overlay |
| **Play / Pause** | *Configurable* | Toggle media playback |
| **Next Track** | *Configurable* | Skip to the next track |
| **Previous Track** | *Configurable* | Return to the previous track |

*All keybindings can be customized in the settings window or directly edited in `config.json`.*

---

## 📁 Configuration File (`%APPDATA%\LucidTune\config.json`)

Settings are stored in `%APPDATA%\LucidTune\config.json`:
```json
{
  "WindowX": 780.0,
  "WindowY": 920.0,
  "PositionPreset": "BottomCenter",
  "WidgetOpacity": 1.0,
  "CompactMode": false,
  "ShowVolumeControl": false,
  "SnapToScreenEdges": true,
  "ExcludeFromCapture": false,
  "AdaptiveColorEnabled": true,
  "AdaptiveBackgroundShimmer": true,
  "TaskbarModeEnabled": false,
  "TaskbarPosition": "Right",
  "ShowAudioSpectrum": true,
  "Language": "en",
  "HotkeyModifiers": "Alt",
  "HotkeyKey": "M",
  "PlayPauseModifiers": "None",
  "PlayPauseKey": "None",
  "NextTrackModifiers": "None",
  "NextTrackKey": "None",
  "PrevTrackModifiers": "None",
  "PrevTrackKey": "None",
  "AutoHideInFullscreen": false,
  "AutoStart": false,
  "ShowSettingsOnStartup": true,
  "DiscordRpcEnabled": true,
  "DiscordIgnoreBrowserVideos": true,
  "DiscordIgnoreTelegramVoice": true
}
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
