using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyNowPlayingOverlay.Models;
using SpotifyNowPlayingOverlay.Services;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;

namespace SpotifyNowPlayingOverlay.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly ISpotifyService _spotifyService;
        private readonly ISettingsService _settingsService;
        private readonly IHotkeyService _hotkeyService;
        private readonly ISmartPollingService _smartPollingService;

        private string _songTitle = "Connecting to Spotify...";
        private string _songArtist = string.Empty;

        private string _artworkA = string.Empty;
        private string _artworkB = string.Empty;
        private bool _isArtworkACurrent = true;
        private bool _isTextVisible = true;

        private long _currentProgressMs;
        private long _durationMs = 1; // Avoid divide by zero
        private double _progressPercentage;
        private string _elapsedTimeText = "0:00";
        private string _durationTimeText = "0:00";
        private long _lastPolledProgressMs;
        private DateTime _lastPolledTime = DateTime.UtcNow;

        private PlaybackStatus _status = PlaybackStatus.NotLoggedIn;
        private bool _isOverlayVisible = true;
        private bool _isManuallyHidden;
        private bool _isLoggedIn;

        // Overlay Style Properties (Synced from Settings)
        private double _scale = 1.0;
        private double _opacity = 0.85;
        private double _fontSize = 14.0;
        private double _songTitleFontSize = 18.0;
        private double _albumSize = 80.0;
        private double _cornerRadius = 12.0;
        private string _accentColor = "#1DB954";
        private bool _alwaysOnTop = true;
        private bool _pinToDesktop = false;
        private bool _clickThrough = false;
        private bool _lockOverlay = false;

        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _autoHideTimer;
        private string _lastTrackId = string.Empty; // Title + Artist to detect changes

        // Commands
        public IRelayCommand LoginCommand { get; }
        public IRelayCommand LogoutCommand { get; }
        public IRelayCommand ToggleLockCommand { get; }
        public IRelayCommand ToggleClickThroughCommand { get; }
        public IRelayCommand ToggleAlwaysOnTopCommand { get; }
        public IRelayCommand CopySongCommand { get; }

        public MainViewModel(
            ISpotifyService spotifyService,
            ISettingsService settingsService,
            IHotkeyService hotkeyService,
            ISmartPollingService smartPollingService)
        {
            _spotifyService = spotifyService;
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;
            _smartPollingService = smartPollingService;

            // Commands
            LoginCommand = new AsyncRelayCommand(LoginAsync);
            LogoutCommand = new RelayCommand(Logout);
            ToggleLockCommand = new RelayCommand(ToggleLock);
            ToggleClickThroughCommand = new RelayCommand(ToggleClickThrough);
            ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);
            CopySongCommand = new RelayCommand(CopySong);

            // Sync Settings initial
            SyncSettings();
            _settingsService.SettingsChanged += (s, e) => SyncSettings();

            // Hook Spotify events
            _spotifyService.PlaybackUpdated += OnPlaybackUpdated;

            // Setup smooth progress bar timer (60 FPS / 16ms ticks)
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            // Setup auto-hide timer (runs every second to count down when paused)
            _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _autoHideTimer.Start();

            // Hook hotkeys
            _hotkeyService.HideOverlayRequested += ToggleHideOverlay;
            _hotkeyService.LockOverlayRequested += ToggleLock;

        }

        #region Properties

        public string SongTitle
        {
            get => _songTitle;
            set => SetProperty(ref _songTitle, value);
        }

        public string SongArtist
        {
            get => _songArtist;
            set => SetProperty(ref _songArtist, value);
        }

        public string ArtworkA
        {
            get => _artworkA;
            set => SetProperty(ref _artworkA, value);
        }

        public string ArtworkB
        {
            get => _artworkB;
            set => SetProperty(ref _artworkB, value);
        }

        public bool IsArtworkACurrent
        {
            get => _isArtworkACurrent;
            set => SetProperty(ref _isArtworkACurrent, value);
        }

        public bool IsTextVisible
        {
            get => _isTextVisible;
            set => SetProperty(ref _isTextVisible, value);
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public string ElapsedTimeText
        {
            get => _elapsedTimeText;
            set => SetProperty(ref _elapsedTimeText, value);
        }

        public string DurationTimeText
        {
            get => _durationTimeText;
            set => SetProperty(ref _durationTimeText, value);
        }

        public PlaybackStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ShowProgressBar));
                }
            }
        }

        public string StatusText => _status switch
        {
            PlaybackStatus.NotLoggedIn => "Connecting to Spotify...",
            PlaybackStatus.Offline => "Spotify Offline",
            PlaybackStatus.NoMusic => "No music playing",
            PlaybackStatus.Paused => "Paused",
            _ => string.Empty
        };

        public bool ShowProgressBar => _status == PlaybackStatus.Playing || _status == PlaybackStatus.Paused;

        public bool IsOverlayVisible
        {
            get => _isOverlayVisible;
            set
            {
                if (SetProperty(ref _isOverlayVisible, value))
                {
                    if (_settingsService.CurrentSettings.IsOverlayVisible != value)
                    {
                        _settingsService.CurrentSettings.IsOverlayVisible = value;
                        _settingsService.SaveSettings();
                    }
                }
            }
        }

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        // Styles
        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public double Opacity
        {
            get => _opacity;
            set
            {
                if (SetProperty(ref _opacity, value))
                {
                    OnPropertyChanged(nameof(BackgroundBrush));
                }
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public double SongTitleFontSize
        {
            get => _songTitleFontSize;
            set => SetProperty(ref _songTitleFontSize, value);
        }

        public double AlbumSize
        {
            get => _albumSize;
            set => SetProperty(ref _albumSize, value);
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set
            {
                if (SetProperty(ref _cornerRadius, value))
                {
                    OnPropertyChanged(nameof(OverlayCornerRadius));
                }
            }
        }

        public System.Windows.Media.Brush BackgroundBrush
        {
            get
            {
                try
                {
                    byte alpha = (byte)(Opacity * 255);
                    var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x1A, 0x1A, 0x1A));
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 0x1A, 0x1A, 0x1A));
                }
            }
        }

        public System.Windows.CornerRadius OverlayCornerRadius => new System.Windows.CornerRadius(CornerRadius);

        public string AccentColor
        {
            get => _accentColor;
            set
            {
                if (SetProperty(ref _accentColor, value))
                {
                    OnPropertyChanged(nameof(AccentColorBrush));
                }
            }
        }

        public System.Windows.Media.Brush AccentColorBrush
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
            get => _alwaysOnTop;
            set
            {
                if (SetProperty(ref _alwaysOnTop, value))
                {
                    OnPropertyChanged(nameof(AlwaysOnTopEffective));
                }
            }
        }

        public bool PinToDesktop
        {
            get => _pinToDesktop;
            set
            {
                if (SetProperty(ref _pinToDesktop, value))
                {
                    OnPropertyChanged(nameof(AlwaysOnTopEffective));
                }
            }
        }

        public bool AlwaysOnTopEffective => AlwaysOnTop && !PinToDesktop;

        public bool ClickThrough
        {
            get => _clickThrough;
            set => SetProperty(ref _clickThrough, value);
        }

        public bool LockOverlay
        {
            get => _lockOverlay;
            set => SetProperty(ref _lockOverlay, value);
        }

        #endregion


        private void SyncSettings()
        {
            var s = _settingsService.CurrentSettings;
            Scale = s.Scale;
            Opacity = s.Opacity;
            FontSize = s.FontSize;
            SongTitleFontSize = s.SongTitleFontSize;
            AlbumSize = s.AlbumSize;
            CornerRadius = s.CornerRadius;
            AccentColor = s.AccentColor;
            AlwaysOnTop = s.AlwaysOnTop;
            PinToDesktop = s.PinToDesktop;
            ClickThrough = s.ClickThrough;
            LockOverlay = s.LockOverlay;
            IsOverlayVisible = s.IsOverlayVisible;
            _isManuallyHidden = !s.IsOverlayVisible;
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (Status == PlaybackStatus.Playing)
            {
                var elapsedSincePoll = (long)(DateTime.UtcNow - _lastPolledTime).TotalMilliseconds;
                _currentProgressMs = _lastPolledProgressMs + elapsedSincePoll;
                if (_currentProgressMs > _durationMs)
                {
                    _currentProgressMs = _durationMs;
                }

                ProgressPercentage = ((double)_currentProgressMs / _durationMs) * 100.0;
                ElapsedTimeText = FormatTime(_currentProgressMs);
            }
        }

        private DateTime _lastPauseTime = DateTime.MinValue;
        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            if (_isManuallyHidden)
            {
                return;
            }

            if (!_settingsService.CurrentSettings.HideWhenPaused)
            {
                if (!IsOverlayVisible && _status != PlaybackStatus.NotLoggedIn)
                {
                    IsOverlayVisible = true;
                }
                return;
            }

            if (Status == PlaybackStatus.Paused || Status == PlaybackStatus.NoMusic || Status == PlaybackStatus.Offline)
            {
                if (_lastPauseTime == DateTime.MinValue)
                {
                    _lastPauseTime = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - _lastPauseTime >= TimeSpan.FromSeconds(_settingsService.CurrentSettings.HideAfterSeconds))
                {
                    IsOverlayVisible = false;
                }
            }
            else
            {
                _lastPauseTime = DateTime.MinValue;
                IsOverlayVisible = true;
            }
        }

        private void OnPlaybackUpdated(object? sender, CurrentlyPlayingState state)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                IsLoggedIn = _spotifyService.IsLoggedIn;

                // Format time strings
                _durationMs = Math.Max(1, state.DurationMs);
                DurationTimeText = FormatTime(state.DurationMs);

                if (state.Status == PlaybackStatus.Playing || state.Status == PlaybackStatus.Paused)
                {
                    _lastPolledProgressMs = state.ProgressMs;
                    _lastPolledTime = DateTime.UtcNow;
                    _currentProgressMs = state.ProgressMs;
                    ProgressPercentage = ((double)_currentProgressMs / _durationMs) * 100.0;
                    ElapsedTimeText = FormatTime(_currentProgressMs);
                }

                Status = state.Status;

                // Track changes detection
                string trackId = $"{state.Title}_{state.Artist}";
                if (trackId != _lastTrackId)
                {
                    _lastTrackId = trackId;
                    await HandleTrackChangeTransitionAsync(state);
                }
                else
                {
                    // Just sync the artwork path if it changes, but without crossfade
                    if (_isArtworkACurrent)
                    {
                        ArtworkA = state.AlbumArtworkPath;
                    }
                    else
                    {
                        ArtworkB = state.AlbumArtworkPath;
                    }
                }
            }));
        }

        private async Task HandleTrackChangeTransitionAsync(CurrentlyPlayingState state)
        {
            // 1. Hide Text (begins fade out storyboard)
            IsTextVisible = false;

            // 2. Set artwork in the inactive slot
            if (_isArtworkACurrent)
            {
                ArtworkB = state.AlbumArtworkPath;
            }
            else
            {
                ArtworkA = state.AlbumArtworkPath;
            }

            // 3. Trigger crossfade storyboard
            IsArtworkACurrent = !IsArtworkACurrent;

            // 4. Wait for fade out to complete (200ms)
            await Task.Delay(200);

            // 5. Update text properties
            if (state.Status == PlaybackStatus.Playing || state.Status == PlaybackStatus.Paused)
            {
                SongTitle = state.Title;
                SongArtist = state.Artist;
            }
            else
            {
                SongTitle = StatusText;
                SongArtist = string.Empty;
            }

            // 6. Show Text (begins fade in storyboard)
            IsTextVisible = true;
        }

        private string FormatTime(long ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            if (t.Hours > 0)
            {
                return $"{t.Hours}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
            return $"{t.Minutes}:{t.Seconds:D2}";
        }

        #region Commands Actions

        private async Task LoginAsync()
        {
            try
            {
                await _spotifyService.StartLoginFlowAsync();
                IsLoggedIn = _spotifyService.IsLoggedIn;
                _smartPollingService.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Spotify Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout()
        {
            _spotifyService.Logout();
            _smartPollingService.Stop();
            IsLoggedIn = false;

            // Clean VM track state
            _lastTrackId = string.Empty;
            SongTitle = StatusText;
            SongArtist = string.Empty;
            ArtworkA = string.Empty;
            ArtworkB = string.Empty;
        }

        private void ToggleLock()
        {
            LockOverlay = !LockOverlay;
            _settingsService.CurrentSettings.LockOverlay = LockOverlay;
            _settingsService.SaveSettings();
        }

        private void ToggleClickThrough()
        {
            ClickThrough = !ClickThrough;
            _settingsService.CurrentSettings.ClickThrough = ClickThrough;
            _settingsService.SaveSettings();
        }

        private void ToggleAlwaysOnTop()
        {
            AlwaysOnTop = !AlwaysOnTop;
            _settingsService.CurrentSettings.AlwaysOnTop = AlwaysOnTop;
            _settingsService.SaveSettings();
        }

        private void ToggleHideOverlay()
        {
            IsOverlayVisible = !IsOverlayVisible;
            _isManuallyHidden = !IsOverlayVisible;
            _settingsService.CurrentSettings.IsOverlayVisible = IsOverlayVisible;
            _settingsService.SaveSettings();
        }

        public void SetManuallyHidden(bool hidden)
        {
            _isManuallyHidden = hidden;
        }

        private void CopySong()
        {
            if (Status == PlaybackStatus.Playing || Status == PlaybackStatus.Paused)
            {
                try
                {
                    Clipboard.SetText($"{SongTitle} - {SongArtist}");
                }
                catch
                {
                    // Clipboard access error
                }
            }
        }

        #endregion
    }
}
