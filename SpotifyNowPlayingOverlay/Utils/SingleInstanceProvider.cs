using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpotifyNowPlayingOverlay.Utils
{
    public static class SingleInstanceProvider
    {
        private static Mutex? _mutex;
        private const string MutexName = "Global\\MusicTrack_Unique_Mutex_Name";

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        public static bool CheckAndRegister()
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                BringExistingToFront();
                return false;
            }
            return true;
        }

        public static void Release()
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
        }

        private static void BringExistingToFront()
        {
            var current = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(current.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id != current.Id)
                {
                    IntPtr hwnd = process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindow(hwnd, SW_RESTORE);
                        SetForegroundWindow(hwnd);
                    }
                    break;
                }
            }
        }
    }
}
