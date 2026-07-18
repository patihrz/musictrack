# Installation and Configuration Guide

This document details the installation, deployment, and configuration options for **MusicTrack** — a Spotify Now Playing desktop overlay.

---

## System Requirements

- **Operating System**: Windows 10 or Windows 11.
- **Runtime**: [.NET 8.0 Runtime (x64) for Windows Desktop](https://dotnet.microsoft.com/download/dotnet/8.0/runtime).
- **Playback Compatibility**: Plays back from any active Spotify device, including Spotify Desktop, Spotify Connect, Mobile, Web Player, and Tablet.

---

## Standard Installation

1. **Download the Release**: Download `MusicTrack.exe` from the [GitHub Releases](../../releases/latest) page, or build the solution locally from the source files using:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false
   ```
2. **Move to Applications Folder**: Copy the output folder to your preferred installation directory (e.g., `C:\Program Files\MusicTrack` or local AppData).
3. **Configure Startup (Optional)**: Open the Settings window (double-click the tray icon) and tick **Start with Windows** to automatically run the application on boot.

---

## Application File Locations

The application creates configuration, log, and cache folders in standard Windows system paths:

- **JSON Configuration**:
  - Path: `%APPDATA%\MusicTrack\settings.json`
  - Purpose: Stores overlay scale, corner radius, fonts, transparency toggles, and Client ID.
- **Secure Refresh Token**:
  - Encrypted utilizing Windows Data Protection API (DPAPI) and saved in the JSON settings as `EncryptedRefreshToken`. It cannot be decrypted on other machines.
- **Downloaded Artwork Files**:
  - Path: `%LOCALAPPDATA%\MusicTrack\ArtworkCache\`
  - Purpose: PNG/JPG downloads of album covers named by their unique Spotify Album ID (e.g., `<SpotifyAlbumId>.jpg`).
- **Artwork Cache Cleanup Rules**:
  - Scans files at startup and every 24 hours.
  - Automatically deletes files that have not been accessed for 90 days.
  - Automatically clears the oldest accessed files if the cache directory exceeds the 500 MB limit.
- **Rotated Logs**:
  - Path: `%APPDATA%\MusicTrack\Logs\`
  - Purpose: Daily logging rotation files (e.g., `log20260704.txt`). Kept for exactly 7 days.

---

## Troubleshooting and FAQ

### The overlay background is black instead of transparent in my streaming software
- **Explanation**: TikTok LIVE Studio or OBS is capturing the window using older graphics capture libraries (like BitBlt) which do not support alpha channels.
- **Solution**: Open the Window Capture properties, and change the Capture Method to **Windows Graphics Capture (WGC)** (WGC is the standard mode for capturing modern transparent windows).

### The overlay is locked and I cannot drag it
- **Explanation**: Click-Through or Lock Position settings are currently enabled.
- **Solution**: Right-click the system tray icon, and untick **Lock Position** or **Click Through**. Alternatively, use the global hotkey shortcuts:
  - `Ctrl + Shift + L`: Unlock position.
  - `Ctrl + Shift + H`: Toggle overlay visibility.

