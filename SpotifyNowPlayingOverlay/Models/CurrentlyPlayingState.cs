using System;

namespace SpotifyNowPlayingOverlay.Models
{
    public enum PlaybackStatus
    {
        NotLoggedIn,
        Offline,     // Spotify application offline or internet disconnected
        NoMusic,     // Online, but nothing is currently playing
        Playing,
        Paused
    }

    public class CurrentlyPlayingState
    {
        public PlaybackStatus Status { get; set; } = PlaybackStatus.NotLoggedIn;
        public string Title { get; set; } = "No music playing";
        public string Artist { get; set; } = string.Empty;
        public string AlbumArtworkPath { get; set; } = string.Empty; // Local file path or fallback
        public string AlbumArtworkUrl { get; set; } = string.Empty;  // Remote URL
        public long ProgressMs { get; set; }
        public long DurationMs { get; set; }
        public bool IsPlaying => Status == PlaybackStatus.Playing;
    }
}
