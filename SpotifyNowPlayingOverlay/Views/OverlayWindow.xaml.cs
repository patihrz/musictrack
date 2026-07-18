using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SpotifyNowPlayingOverlay.Services;
using SpotifyNowPlayingOverlay.ViewModels;
using SpotifyNowPlayingOverlay.Utils;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SpotifyNowPlayingOverlay.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IHotkeyService _hotkeyService;
        private bool _isSnapping;

        public OverlayWindow(
            MainViewModel viewModel,
            ISettingsService settingsService,
            IHotkeyService hotkeyService)
        {
            InitializeComponent();

            DataContext = viewModel;
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;

            // Restore position
            Left = _settingsService.CurrentSettings.WindowLeft;
            Top = _settingsService.CurrentSettings.WindowTop;

            // Ensure window starts in a valid screen area
            EnsurePositionInBounds();

            // Subscribe to VM properties
            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Subscribe to settings hotkey event
            _hotkeyService.ShowSettingsRequested += HotkeyService_ShowSettingsRequested;

            // Subscribe to settings changes for streamer mode
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;

            Loaded += OverlayWindow_Loaded;
            LocationChanged += Window_LocationChanged;
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initial style checks
            UpdateClickThrough();
            UpdateVisibility();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            // Hook window messages for hotkeys
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(HwndHook);

            // Register Hotkeys
            _hotkeyService.RegisterHotkeys(hwnd);

            // Set styles
            UpdateClickThrough();
            UpdatePinToDesktop();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            _hotkeyService.HandleWndProc(hwnd, msg, wParam, lParam, ref handled);

            if (msg == Win32Helper.WM_WINDOWPOSCHANGING && DataContext is MainViewModel vm && vm.PinToDesktop)
            {
                try
                {
                    var wp = (Win32Helper.WINDOWPOS)System.Runtime.InteropServices.Marshal.PtrToStructure(lParam, typeof(Win32Helper.WINDOWPOS))!;
                    wp.hwndInsertAfter = Win32Helper.HWND_BOTTOM;
                    wp.flags |= Win32Helper.SWP_NOACTIVATE;
                    System.Runtime.InteropServices.Marshal.StructureToPtr(wp, lParam, false);
                }
                catch
                {
                    // Ignored
                }
            }

            return IntPtr.Zero;
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (_isSnapping) return;
            if (Left < -10000 || Top < -10000) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            try
            {
                var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                var workingArea = screen.WorkingArea;

                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                var presentationSource = PresentationSource.FromVisual(this);
                if (presentationSource?.CompositionTarget != null)
                {
                    dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
                }

                double screenLeft = workingArea.Left / dpiScaleX;
                double screenTop = workingArea.Top / dpiScaleY;
                double screenRight = workingArea.Right / dpiScaleX;
                double screenBottom = workingArea.Bottom / dpiScaleY;

                const double SnapThreshold = 15.0;
                double newLeft = Left;
                double newTop = Top;

                // Horizontal Snap
                if (Math.Abs(Left - screenLeft) < SnapThreshold)
                {
                    newLeft = screenLeft;
                }
                else if (Math.Abs((Left + Width) - screenRight) < SnapThreshold)
                {
                    newLeft = screenRight - Width;
                }

                // Vertical Snap
                if (Math.Abs(Top - screenTop) < SnapThreshold)
                {
                    newTop = screenTop;
                }
                else if (Math.Abs((Top + Height) - screenBottom) < SnapThreshold)
                {
                    newTop = screenBottom - Height;
                }

                if (Math.Abs(newLeft - Left) > 0.01 || Math.Abs(newTop - Top) > 0.01)
                {
                    _isSnapping = true;
                    Left = newLeft;
                    Top = newTop;
                    _isSnapping = false;

                    // Save position
                    _settingsService.CurrentSettings.WindowLeft = Left;
                    _settingsService.CurrentSettings.WindowTop = Top;
                    _settingsService.SaveSettings();
                }
            }
            catch
            {
                // Ignored
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.ClickThrough))
            {
                Dispatcher.Invoke(UpdateClickThrough);
            }
            else if (e.PropertyName == nameof(MainViewModel.IsOverlayVisible))
            {
                Dispatcher.Invoke(UpdateVisibility);
            }
            else if (e.PropertyName == nameof(MainViewModel.PinToDesktop))
            {
                Dispatcher.Invoke(UpdatePinToDesktop);
            }
        }

        private void UpdateClickThrough()
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            IntPtr extendedStyle = Win32Helper.GetWindowLongPtr(hwnd, Win32Helper.GWL_EXSTYLE);

            if (DataContext is MainViewModel vm && vm.ClickThrough)
            {
                extendedStyle = new IntPtr(extendedStyle.ToInt64() | Win32Helper.WS_EX_TRANSPARENT);
            }
            else
            {
                extendedStyle = new IntPtr(extendedStyle.ToInt64() & ~Win32Helper.WS_EX_TRANSPARENT);
            }

            Win32Helper.SetWindowLongPtr(hwnd, Win32Helper.GWL_EXSTYLE, extendedStyle);
        }

        private void UpdatePinToDesktop()
        {
            if (DataContext is MainViewModel vm && vm.PinToDesktop)
            {
                SendToBottom();
            }
        }

        private void SendToBottom()
        {
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd != IntPtr.Zero)
            {
                Win32Helper.SetWindowPos(hwnd, Win32Helper.HWND_BOTTOM, 0, 0, 0, 0, Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE | Win32Helper.SWP_NOACTIVATE);
            }
        }

        private void SettingsService_SettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(UpdateVisibility);
        }

        private void UpdateVisibility()
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.IsOverlayVisible)
                {
                    Show();
                }
                else
                {
                    Hide();
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && !vm.LockOverlay)
            {
                try
                {
                    DragMove();

                    // Save new location
                    if (!(Left < -10000 || Top < -10000))
                    {
                        _settingsService.CurrentSettings.WindowLeft = Left;
                        _settingsService.CurrentSettings.WindowTop = Top;
                        _settingsService.SaveSettings();
                    }
                }
                catch
                {
                    // Ignored (e.g. double click or drag error)
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = (App)Application.Current;
                app.OpenSettingsWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error opening settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsOverlayVisible = false;
                vm.SetManuallyHidden(true);
            }
        }

        private void EnsurePositionInBounds()
        {
            // If the saved coordinates put it off-screen, reset to standard top-left
            if (Left < 0 || Left > SystemParameters.VirtualScreenWidth - 100 ||
                Top < 0 || Top > SystemParameters.VirtualScreenHeight - 100)
            {
                Left = 100;
                Top = 100;
            }
        }

        private void HotkeyService_ShowSettingsRequested()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var app = (App)Application.Current;
                    app.OpenSettingsWindow();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error opening settings", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hotkeyService.UnregisterHotkeys(helper.Handle);
            _hotkeyService.ShowSettingsRequested -= HotkeyService_ShowSettingsRequested;
            _settingsService.SettingsChanged -= SettingsService_SettingsChanged;

            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            base.OnClosed(e);
        }
    }
}
