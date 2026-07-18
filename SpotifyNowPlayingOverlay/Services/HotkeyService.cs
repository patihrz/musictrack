using System;
using SpotifyNowPlayingOverlay.Utils;

namespace SpotifyNowPlayingOverlay.Services
{
    public class HotkeyService : IHotkeyService
    {
        public event Action? HideOverlayRequested;
        public event Action? LockOverlayRequested;
        public event Action? ShowSettingsRequested;

        private IntPtr _hWnd = IntPtr.Zero;

        public void RegisterHotkeys(IntPtr hWnd)
        {
            _hWnd = hWnd;

            // Register Ctrl+Shift+H
            Win32Helper.RegisterHotKey(
                _hWnd,
                Win32Helper.HOTKEY_HIDE_ID,
                Win32Helper.MOD_CONTROL | Win32Helper.MOD_SHIFT,
                Win32Helper.VK_H
            );

            // Register Ctrl+Shift+L
            Win32Helper.RegisterHotKey(
                _hWnd,
                Win32Helper.HOTKEY_LOCK_ID,
                Win32Helper.MOD_CONTROL | Win32Helper.MOD_SHIFT,
                Win32Helper.VK_L
            );

            // Register Ctrl+Shift+S
            Win32Helper.RegisterHotKey(
                _hWnd,
                Win32Helper.HOTKEY_SETTINGS_ID,
                Win32Helper.MOD_CONTROL | Win32Helper.MOD_SHIFT,
                Win32Helper.VK_S
            );
        }

        public void UnregisterHotkeys(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                Win32Helper.UnregisterHotKey(hWnd, Win32Helper.HOTKEY_HIDE_ID);
                Win32Helper.UnregisterHotKey(hWnd, Win32Helper.HOTKEY_LOCK_ID);
                Win32Helper.UnregisterHotKey(hWnd, Win32Helper.HOTKEY_SETTINGS_ID);
            }
        }

        public void HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Helper.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (hotkeyId == Win32Helper.HOTKEY_HIDE_ID)
                {
                    HideOverlayRequested?.Invoke();
                    handled = true;
                }
                else if (hotkeyId == Win32Helper.HOTKEY_LOCK_ID)
                {
                    LockOverlayRequested?.Invoke();
                    handled = true;
                }
                else if (hotkeyId == Win32Helper.HOTKEY_SETTINGS_ID)
                {
                    ShowSettingsRequested?.Invoke();
                    handled = true;
                }
            }
        }
    }
}
