using System;

namespace SpotifyNowPlayingOverlay.Models
{
    public class AppSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 0.85;
        public double FontSize { get; set; } = 14.0;
        public double SongTitleFontSize { get; set; } = 18.0;
        public double AlbumSize { get; set; } = 80.0;
        public double CornerRadius { get; set; } = 12.0;
        public string AccentColor { get; set; } = "#1DB954";
        public bool AlwaysOnTop { get; set; } = true;
        public bool ClickThrough { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public double PollIntervalSeconds { get; set; } = 1.0;
        public double HideAfterSeconds { get; set; } = 10.0;
        public bool HideWhenPaused { get; set; } = false;
        public bool PinToDesktop { get; set; } = false;
        public bool IsOverlayVisible { get; set; } = true;
        public bool LockOverlay { get; set; } = false;
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public string EncryptedRefreshToken { get; set; } = string.Empty;
    }
}
