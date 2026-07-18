using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyNowPlayingOverlay.Services;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.ViewModels
{
    public class SettingsViewModel : ObservableObject, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly ISpotifyService _spotifyService;

        public IAsyncRelayCommand LoginCommand { get; }
        public IRelayCommand LogoutCommand { get; }

        public SettingsViewModel(ISettingsService settingsService, ISpotifyService spotifyService)
        {
            _settingsService = settingsService;
            _spotifyService = spotifyService;

            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new RelayCommand(Logout);

            _settingsService.SettingsChanged += SettingsService_SettingsChanged;
            _spotifyService.PlaybackUpdated += SpotifyService_PlaybackUpdated;
        }

        private void SettingsService_SettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(string.Empty); // Refresh all settings properties
        }

        private void SpotifyService_PlaybackUpdated(object? sender, CurrentlyPlayingState e)
        {
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(StatusText));
        }

        private async Task LoginAsync()
        {
            try
            {
                await _spotifyService.StartLoginFlowAsync();
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(StatusText));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to login from settings window");
            }
        }

        private void Logout()
        {
            _spotifyService.Logout();
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(StatusText));
        }

        public void Dispose()
        {
            _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
            _spotifyService.PlaybackUpdated -= SpotifyService_PlaybackUpdated;
        }

        public string ClientId
        {
            get => _settingsService.CurrentSettings.ClientId;
            set
            {
                if (_settingsService.CurrentSettings.ClientId != value)
                {
                    _settingsService.CurrentSettings.ClientId = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double Scale
        {
            get => _settingsService.CurrentSettings.Scale;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.Scale - value) > 0.01)
                {
                    _settingsService.CurrentSettings.Scale = Math.Clamp(value, 0.5, 3.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double Opacity
        {
            get => _settingsService.CurrentSettings.Opacity;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.Opacity - value) > 0.01)
                {
                    _settingsService.CurrentSettings.Opacity = Math.Clamp(value, 0.1, 1.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double FontSize
        {
            get => _settingsService.CurrentSettings.FontSize;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.FontSize - value) > 0.1)
                {
                    _settingsService.CurrentSettings.FontSize = Math.Clamp(value, 8.0, 36.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double SongTitleFontSize
        {
            get => _settingsService.CurrentSettings.SongTitleFontSize;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.SongTitleFontSize - value) > 0.1)
                {
                    _settingsService.CurrentSettings.SongTitleFontSize = Math.Clamp(value, 8.0, 48.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double AlbumSize
        {
            get => _settingsService.CurrentSettings.AlbumSize;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.AlbumSize - value) > 0.1)
                {
                    _settingsService.CurrentSettings.AlbumSize = Math.Clamp(value, 40.0, 200.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double CornerRadius
        {
            get => _settingsService.CurrentSettings.CornerRadius;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.CornerRadius - value) > 0.1)
                {
                    _settingsService.CurrentSettings.CornerRadius = Math.Clamp(value, 0.0, 40.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public string AccentColor
        {
            get => _settingsService.CurrentSettings.AccentColor;
            set
            {
                if (_settingsService.CurrentSettings.AccentColor != value)
                {
                    _settingsService.CurrentSettings.AccentColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccentBrush));
                    _settingsService.SaveSettings();
                }
            }
        }

        public System.Windows.Media.Brush AccentBrush
        {
            get
            {
                try
                {
                    var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(AccentColor)!;
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return System.Windows.Media.Brushes.MediumSeaGreen;
                }
            }
        }

        public bool AlwaysOnTop
        {
            get => _settingsService.CurrentSettings.AlwaysOnTop;
            set
            {
                if (_settingsService.CurrentSettings.AlwaysOnTop != value)
                {
                    _settingsService.CurrentSettings.AlwaysOnTop = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool PinToDesktop
        {
            get => _settingsService.CurrentSettings.PinToDesktop;
            set
            {
                if (_settingsService.CurrentSettings.PinToDesktop != value)
                {
                    _settingsService.CurrentSettings.PinToDesktop = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool ClickThrough
        {
            get => _settingsService.CurrentSettings.ClickThrough;
            set
            {
                if (_settingsService.CurrentSettings.ClickThrough != value)
                {
                    _settingsService.CurrentSettings.ClickThrough = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool StartWithWindows
        {
            get => _settingsService.CurrentSettings.StartWithWindows;
            set
            {
                if (_settingsService.CurrentSettings.StartWithWindows != value)
                {
                    _settingsService.CurrentSettings.StartWithWindows = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double PollIntervalSeconds
        {
            get => _settingsService.CurrentSettings.PollIntervalSeconds;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.PollIntervalSeconds - value) > 0.1)
                {
                    _settingsService.CurrentSettings.PollIntervalSeconds = Math.Clamp(value, 0.5, 10.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public double HideAfterSeconds
        {
            get => _settingsService.CurrentSettings.HideAfterSeconds;
            set
            {
                if (Math.Abs(_settingsService.CurrentSettings.HideAfterSeconds - value) > 0.1)
                {
                    _settingsService.CurrentSettings.HideAfterSeconds = Math.Clamp(value, 1.0, 120.0);
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool HideWhenPaused
        {
            get => _settingsService.CurrentSettings.HideWhenPaused;
            set
            {
                if (_settingsService.CurrentSettings.HideWhenPaused != value)
                {
                    _settingsService.CurrentSettings.HideWhenPaused = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool LockOverlay
        {
            get => _settingsService.CurrentSettings.LockOverlay;
            set
            {
                if (_settingsService.CurrentSettings.LockOverlay != value)
                {
                    _settingsService.CurrentSettings.LockOverlay = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }


        public bool IsOverlayVisible
        {
            get => _settingsService.CurrentSettings.IsOverlayVisible;
            set
            {
                if (_settingsService.CurrentSettings.IsOverlayVisible != value)
                {
                    _settingsService.CurrentSettings.IsOverlayVisible = value;
                    OnPropertyChanged();
                    _settingsService.SaveSettings();
                }
            }
        }

        public bool IsLoggedIn => _spotifyService.IsLoggedIn;

        public string StatusText => _spotifyService.IsLoggedIn ? "Connected" : "Not Connected";

        public void Reload()
        {
            _settingsService.LoadSettings();
            OnPropertyChanged(string.Empty); // Notifies all properties
        }
    }
}
