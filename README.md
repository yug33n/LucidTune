# LucidTune

LucidTune is a lightweight desktop overlay widget for Windows 10 / 11 that displays currently playing media metadata and provides playback controls directly above applications and games.

## Features

- **Windows SMTC Integration**: Automatically tracks active media sessions (Spotify, Yandex Music, Chrome, Edge, Firefox, Telegram, etc.).
- **Live Metadata & Album Art**: Displays track title, artist name, dynamic progress bar, and album artwork with a neutral fallback icon.
- **Playback Controls**: Play/Pause, Next, and Previous controls with vector SVG icons and smooth hover animations.
- **Floating Glass Island Design**: Minimalist frosted glass aesthetic (`#D9111114`, 18px corner radius, soft diffused drop shadow).
- **Global Hotkey Support**: Default hotkey `Alt + M` to toggle visibility on demand.
- **System Tray Integration**: Background execution with custom context menu for toggling visibility, hotkey customization, autostart, and exit.
- **Position Persistence**: Automatically saves and restores overlay screen coordinates with display boundary validation.
- **Fullscreen / Game Auto-Hide**: Optional automatic hiding when entering fullscreen games and applications.
- **Minimal Resource Footprint**: Optimized background event handling and idle timers for low memory usage and 0% idle CPU.

## Requirements

- Windows 10 (Build 19041+) or Windows 11 (x64)
- .NET 9.0 Desktop Runtime (or self-contained build)

## Build Instructions

### Standard Build
```powershell
dotnet build -c Release
```

### Self-Contained Executable
To publish a standalone executable without requiring a pre-installed .NET runtime:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## Configuration

Settings are saved in `config.json`:
```json
{
  "WindowX": 780.0,
  "WindowY": 920.0,
  "HotkeyModifiers": "Alt",
  "HotkeyKey": "M",
  "AutoHideInFullscreen": false,
  "AutoStart": false
}
```
Hotkeys and startup preferences can also be configured directly via the tray icon context menu.
