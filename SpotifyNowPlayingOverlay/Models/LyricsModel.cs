using System;
using System.Collections.Generic;

namespace SpotifyNowPlayingOverlay.Models
{
    public struct LyricsLine
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; }

        public LyricsLine(TimeSpan timestamp, string text)
        {
            Timestamp = timestamp;
            Text = text;
        }
    }

    public class LyricsResult
    {
        public bool Found { get; set; }
        public bool IsSynced { get; set; }
        public List<LyricsLine> SyncedLines { get; set; } = new();
        public string PlainLyrics { get; set; } = string.Empty;
    }
}
