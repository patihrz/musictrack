using System;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface IHotkeyService
    {
        void RegisterHotkeys(IntPtr hWnd);
        void UnregisterHotkeys(IntPtr hWnd);
        event Action? HideOverlayRequested;
        event Action? LockOverlayRequested;
        event Action? ShowSettingsRequested;
        void HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    }
}
