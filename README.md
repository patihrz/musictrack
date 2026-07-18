# MusicTrack

[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%20%2F%2092%20%E2%9C%94-brightgreen?logo=virustotal&logoColor=white)](https://www.virustotal.com/gui/url/b27d3ab1a8a9595b1696a239eec62ee0d9233b889406bef94af4844d7fa85bcc)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue?logo=windows&logoColor=white)](https://github.com/patihrz/musictrack/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/patihrz/musictrack?color=1DB954&logo=github)](https://github.com/patihrz/musictrack/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/patihrz/musictrack/total?color=1DB954)](https://github.com/patihrz/musictrack/releases)

A lightweight, high-performance, borderless transparent Windows desktop overlay for displaying the currently playing Spotify song. Designed specifically for streamers to capture using **TikTok LIVE Studio** (or OBS) via Window Capture without requiring browser sources or external web services.

---

## Features

- **OAuth PKCE Connection**: Secure connection directly with Spotify without a backend.
- **Hardware-Accelerated Transitions**: Smooth 200ms fade-in/fade-out animations on song change.
- **Album Artwork Crossfade**: Seamlessly crossfades covers during transitions.
- **System Tray Icon**: Custom tray context menu for locking positions, toggling click-through, opening settings, copying songs, and logging in/out.
- **WPF Native Transparency**: Borderless overlay captured perfectly in Window Capture modes.
- **Global Hotkeys**:
  - `Ctrl + Shift + H`: Hide / Show the overlay.
  - `Ctrl + Shift + L`: Lock / Unlock the overlay position (draggable while unlocked).
- **Responsive Customizations**:
  - Adjustable scale, background opacity, corners radius, album size, and font sizes.
  - Predefined or custom hex accent colors (Spotify Green by default).
  - Auto-hide overlay after configurable seconds when music is paused.
  - High-frequency local timer for smooth progress bar updates.
- **Performance Optimized**: Built using WPF Native .NET 8, maintaining `<1%` CPU and `<50MB` RAM.

---

## Spotify Developer Setup (Mandatory First Step)

Because this is a standalone desktop application that communicates directly with Spotify, you must obtain a **Client ID** from Spotify's Developer Dashboard.

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/) and log in with your Spotify account.
2. Click **Create App**.
3. Fill in the App details:
   - **App Name**: `MusicTrack`
   - **App Description**: `Stream overlay for Spotify.`
   - **Redirect URIs**: **`http://127.0.0.1:8888/callback`** *(Must match this EXACTLY, without trailing slash)*
4. Under **Which API/SDKs are you planning to use?**, select **Web API**.
5. Read and accept the terms, then click **Save**.
6. Once created, open your App and click **Settings**.
7. Copy the **Client ID** (you will paste this into MusicTrack's Settings window).

---

## Build & Run Instructions

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
- Visual Studio 2022 (with the `.NET Desktop Development` workload) or VS Code.

### Building via Visual Studio
1. Open the solution file `SpotifyNowPlayingOverlay.sln` in Visual Studio.
2. Set the build configuration to **Release** and platform to **Any CPU**.
3. Right-click the solution and select **Restore NuGet Packages**.
4. Press `F5` to run, or go to **Build > Build Solution** to generate the executable.

### Building via Command Line (CLI)
Navigate to the root workspace directory and run:
```bash
# Restore packages
dotnet restore

# Build release binary
dotnet build --configuration Release

# Run the app
dotnet run --project SpotifyNowPlayingOverlay
```
The compiled binary `MusicTrack.exe` will be output to `SpotifyNowPlayingOverlay/bin/Release/net8.0-windows/`.

---

## How to Use

1. **Launch** `MusicTrack.exe`.
2. You will see a small tray icon in your Windows Taskbar (a Spotify-green circle with a dark play triangle inside).
3. **Double-click** the tray icon, or right-click it and choose **Settings...**
4. Enter your **Spotify Client ID** in the box.
5. Click **Connect Spotify**.
6. A web page will open in your browser asking you to authorize the app. Click **Agree**.
7. You should see "Authentication Successful!". You can close the browser window.
8. Start playing music on Spotify. The overlay will immediately show up on your desktop!
9. Right-click the tray icon to:
   - Lock/Unlock dragging.
   - Enable "Click Through" (which makes the overlay ignore mouse clicks, so you can interact with windows behind it).
   - Copy the currently playing track name and artist.

---

## Stream Capture Setup (TikTok LIVE Studio)

To add the overlay to your livestream layout:

1. Open **TikTok LIVE Studio**.
2. Add a new source and select **Window Capture**.
3. In the Window dropdown, select **`[MusicTrack.exe]: MusicTrack`**.
4. Ensure the capture mode is set to **Windows Graphics Capture (WGC)** (this is crucial for transparent windows).
5. Tick **Capture Transparency / Allows Alpha** if the checkbox is available.
6. The overlay will show up with a perfectly transparent background on your stream feed!

---

## 🔒 Security & Privacy

[![VirusTotal](https://img.shields.io/badge/VirusTotal-0%20%2F%2092%20%E2%9C%94-brightgreen?logo=virustotal&logoColor=white)](https://www.virustotal.com/gui/url/b27d3ab1a8a9595b1696a239eec62ee0d9233b889406bef94af4844d7fa85bcc)

MusicTrack is **100% open source** and has been scanned clean by **92 antivirus engines** on VirusTotal.

| | Details |
|---|---|
| 🛡️ **VirusTotal Result** | [0 / 92 — Undetected ✔](https://www.virustotal.com/gui/url/b27d3ab1a8a9595b1696a239eec62ee0d9233b889406bef94af4844d7fa85bcc) |
| 🔐 **Authentication** | OAuth 2.0 PKCE — your Spotify credentials are never seen by this app |
| 🔑 **Token Storage** | Refresh token encrypted via Windows DPAPI (machine-locked, never leaves your PC) |
| 🌐 **Network** | Only connects to `api.spotify.com` — no telemetry, no tracking, no ads |
| 💾 **Data Stored Locally** | Settings JSON + artwork cache in `%APPDATA%\MusicTrack\` |

> **Why might antivirus flag WPF apps?** Some AV tools flag any obfuscated or self-contained .NET EXE as "suspicious" by heuristic. MusicTrack uses [Obfuscar](https://github.com/obfuscar/obfuscar) for code protection, which is standard practice. The VirusTotal scan above confirms **0 real detections**.
