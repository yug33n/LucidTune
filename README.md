# LucidTune 🎵

**English** | [Русский](README_RU.md)

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg?logo=windows)](https://microsoft.com/windows)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0%20WPF-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release: v1.0.0](https://img.shields.io/badge/Release-v1.0.0-4E75FF.svg)](https://github.com/yug33n/LucidTune/releases)

**LucidTune** is an ultra-lightweight, customizable desktop overlay widget (Dynamic Island style) for Windows 10 and 11. It brings real-time media metadata, album artwork, track playback progress, and media controls directly above your apps and games with a frosted glass aesthetic and customizable global hotkeys.

---

## ✨ Features

### 🎶 Windows Media (SMTC) Integration
- Seamlessly synchronizes with Windows **System Media Transport Controls**.
- Automatically captures media sessions from **Spotify, YouTube Music / Chrome, Edge, Firefox, Telegram, VLC, Yandex Music, AIMP**, and any other SMTC-compatible media player.
- Live track titles, artist details, dynamic progress bar, and crisp album art with an elegant vector fallback.
- Rapid track switching safety guards (debounce protection preventing null-reference crashes).

### 🎛️ Elegant Dynamic Island Overlay
- **Frosted Glass Island Design**: Modern deep dark glass palette (`#D9111114`), smooth 18px corner radius, and neon blue accents (`#4E75FF`).
- **Balanced Playback Controls**: Pixel-perfect Previous, Play/Pause, and Next buttons with matching stroke weights and fluid hover states.
- **Cinematic Taskbar Minimize Animation**: Smooth slide-down & scale transition when hiding into the taskbar, and an upward slide reveal when showing.
- **Hover-Reveal Close Button**: Floating circular button emerges gracefully from the darkness when hovering over the widget and disappears when unhovered.
- **Real-Time Opacity Slider**: Adjust widget transparency from 20% to 100% with live overlay feedback.

### ⚙️ Rich Configuration Window
- **Position Presets**: Instantly dock the widget to Top Left, Top Center, Top Right, Bottom Left, Bottom Center, Bottom Right, or Custom coordinates with automated display boundary clamping.
- **Global Hotkey Customization**:
  - Show / Hide widget (defaults to `Alt + M`).
  - Play / Pause.
  - Next Track.
  - Previous Track.
  - Full support for `Ctrl`, `Alt`, `Shift`, and `Win` modifiers.
- **Custom Dark Controls**: Handcrafted dark dropdown menus (ComboBox), custom scrollbars, and focus rings.
- **Fullscreen & Game Detection**: Optional automatic hiding when launching fullscreen games or applications.
- **System Integration**: Toggle launch on Windows startup and startup window display.

### 🔔 System Tray & Brand Icon
- Mathematically centered vector music note inside a circular dark badge, packaged in multi-resolution ICO (16×16 to 256×256) for sharp rendering at all DPI scales.
- Dark context menu without the default Windows white gutter, featuring accent checkmarks and a dynamic "Show / Hide" state.
- Single-click toggle: click the tray icon to quickly show or hide the overlay.

---

## 🖥️ System Requirements

- **OS**: Windows 10 (Build 19041+) or Windows 11 (x64)
- **Runtime**:
  - **Standard Build**: [.NET 9.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)
  - **Standalone Build (Self-Contained)**: No external dependencies or runtime required.

---

## 🚀 Installation & Running

### Option 1. Standalone Single-File Executable
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

## 📁 Configuration File (`config.json`)

Settings are stored in `config.json` alongside the executable or working directory:
```json
{
  "WindowX": 780.0,
  "WindowY": 920.0,
  "PositionPreset": "BottomCenter",
  "WidgetOpacity": 1.0,
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
  "ShowSettingsOnStartup": true
}
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
