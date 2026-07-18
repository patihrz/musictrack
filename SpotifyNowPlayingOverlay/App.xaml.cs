using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SpotifyNowPlayingOverlay.Services;
using SpotifyNowPlayingOverlay.ViewModels;
using SpotifyNowPlayingOverlay.Views;
using SpotifyNowPlayingOverlay.Utils;
using Serilog;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SpotifyNowPlayingOverlay
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;
        private H.NotifyIcon.TaskbarIcon? _taskbarIcon;
        private SettingsWindow? _settingsWindow;

        public IServiceProvider ServiceProvider => _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Single Instance Check
            if (!SingleInstanceProvider.CheckAndRegister())
            {
                Shutdown();
                return;
            }

            // 2. Initialize Logging (Serilog)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logPath = Path.Combine(appData, "MusicTrack", "Logs", "log.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            Log.Information("MusicTrack starting up.");

            base.OnStartup(e);

            try
            {
                // 3. Dependency Injection Configuration
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // 4. Load Settings & Cache Cleanup Initialization
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                settingsService.LoadSettings();

                var cacheManager = _serviceProvider.GetRequiredService<IArtworkCacheManager>();
                cacheManager.StartCleanupWorker();

                // 6. Initialize and Show Overlay Window
                var overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
                overlayWindow.Show();

                // 7. Initialize System Tray Icon
                InitializeTrayIcon();

                // 8. Start Smart Polling
                var pollingService = _serviceProvider.GetRequiredService<ISmartPollingService>();
                pollingService.Start();

                // 9. Run Startup Spotify Login flow
                _ = InitializeSpotifyAuthAsync();

                Log.Information("Application startup completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed.");
                MessageBox.Show($"Startup Error: {ex.Message}", "Critical Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private async Task InitializeSpotifyAuthAsync()
        {
            try
            {
                var spotifyService = _serviceProvider.GetRequiredService<ISpotifyService>();
                var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();

                Log.Information("Orchestrating Spotify startup login sequence.");

                // 1. Try Auto Login
                bool autoLoginSuccess = await spotifyService.TryAutoLoginAsync();
                
                // 2. If it fails, start OAuth login flow (opens browser)
                if (!autoLoginSuccess)
                {
                    Log.Information("Auto-login failed. Initiating OAuth browser login flow.");
                    await spotifyService.StartLoginFlowAsync();
                }

                // Update IsLoggedIn in VM
                mainVM.IsLoggedIn = spotifyService.IsLoggedIn;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in InitializeSpotifyAuthAsync.");
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // HTTP Client Singleton
            services.AddSingleton(new HttpClient());

            // Core Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IArtworkCacheManager, ArtworkCacheManager>();
            services.AddSingleton<ISpotifyService, SpotifyService>();
            services.AddSingleton<ISmartPollingService, SmartPollingService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Views
            services.AddSingleton<OverlayWindow>();
            services.AddTransient<SettingsWindow>();
        }

        private void InitializeTrayIcon()
        {
            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();

            _taskbarIcon = new H.NotifyIcon.TaskbarIcon();
            _taskbarIcon.Icon = CreateStatusIcon();
            _taskbarIcon.ToolTipText = "MusicTrack";

            // Double-click tray icon to open settings
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => OpenSettingsWindow();

            // Create Context Menu
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var loginItem = new System.Windows.Controls.MenuItem { Header = "Connect Spotify..." };
            loginItem.SetBinding(System.Windows.Controls.MenuItem.CommandProperty, new System.Windows.Data.Binding("LoginCommand"));
            loginItem.SetBinding(System.Windows.Controls.MenuItem.IsEnabledProperty, new System.Windows.Data.Binding("IsLoggedIn") { Converter = new Converters.InverseBooleanConverter() });

            var logoutItem = new System.Windows.Controls.MenuItem { Header = "Disconnect" };
            logoutItem.SetBinding(System.Windows.Controls.MenuItem.CommandProperty, new System.Windows.Data.Binding("LogoutCommand"));
            logoutItem.SetBinding(System.Windows.Controls.MenuItem.IsEnabledProperty, new System.Windows.Data.Binding("IsLoggedIn"));

            var copyItem = new System.Windows.Controls.MenuItem { Header = "Copy Current Song" };
            copyItem.SetBinding(System.Windows.Controls.MenuItem.CommandProperty, new System.Windows.Data.Binding("CopySongCommand"));

            var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings..." };
            settingsItem.Click += (s, e) => OpenSettingsWindow();

            var alwaysOnTopItem = new System.Windows.Controls.MenuItem { Header = "Always On Top", IsCheckable = true };
            alwaysOnTopItem.SetBinding(System.Windows.Controls.MenuItem.IsCheckedProperty, new System.Windows.Data.Binding("AlwaysOnTop") { Mode = System.Windows.Data.BindingMode.TwoWay });

            var clickThroughItem = new System.Windows.Controls.MenuItem { Header = "Click Through", IsCheckable = true };
            clickThroughItem.SetBinding(System.Windows.Controls.MenuItem.IsCheckedProperty, new System.Windows.Data.Binding("ClickThrough") { Mode = System.Windows.Data.BindingMode.TwoWay });

            var lockItem = new System.Windows.Controls.MenuItem { Header = "Lock Position", IsCheckable = true };
            lockItem.SetBinding(System.Windows.Controls.MenuItem.IsCheckedProperty, new System.Windows.Data.Binding("LockOverlay") { Mode = System.Windows.Data.BindingMode.TwoWay });

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => Shutdown();

            contextMenu.Items.Add(loginItem);
            contextMenu.Items.Add(logoutItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(copyItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(alwaysOnTopItem);
            contextMenu.Items.Add(clickThroughItem);
            contextMenu.Items.Add(lockItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = contextMenu;
            _taskbarIcon.DataContext = mainVM;

            try
            {
                _taskbarIcon.ForceCreate();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to force create tray icon. Falling back to overlay settings button.");
            }
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }

            var settingsVM = _serviceProvider.GetRequiredService<SettingsViewModel>();
            settingsVM.Reload();
            _settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            _settingsWindow.DataContext = settingsVM;
            _settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.Focus();

            // Force to foreground
            _settingsWindow.Topmost = true;
            _settingsWindow.Topmost = false;
        }

        private System.Drawing.Icon CreateStatusIcon()
        {
            using var bitmap = new System.Drawing.Bitmap(16, 16);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(29, 185, 84)); // Spotify Green
                g.FillEllipse(brush, 0, 0, 15, 15);

                using var darkBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(18, 18, 18));
                var points = new System.Drawing.Point[]
                {
                    new System.Drawing.Point(6, 4),
                    new System.Drawing.Point(6, 11),
                    new System.Drawing.Point(11, 7)
                };
                g.FillPolygon(darkBrush, points);
            }

            IntPtr hIcon = bitmap.GetHicon();
            var icon = System.Drawing.Icon.FromHandle(hIcon);
            return icon;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutting down.");

            try
            {
                if (_serviceProvider != null)
                {
                    var cacheManager = _serviceProvider.GetService<IArtworkCacheManager>();
                    cacheManager?.StopCleanupWorker();

                    var pollingService = _serviceProvider.GetService<ISmartPollingService>();
                    pollingService?.Stop();
                }
            }
            catch { }

            _taskbarIcon?.Dispose();
            SingleInstanceProvider.Release();
            Log.CloseAndFlush();

            base.OnExit(e);
        }
    }
}
