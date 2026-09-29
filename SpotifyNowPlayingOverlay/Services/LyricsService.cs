using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public class LyricsService : ILyricsService
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, LyricsResult> _cache = new();
        private static readonly Regex LrcTimeRegex = new(@"^\[(\d+):(\d+(?:\.\d+)?)\](.*)$", RegexOptions.Compiled);

        public LyricsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<LyricsResult> GetLyricsAsync(string trackName, string artistName, string albumName, double durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(trackName))
            {
                return new LyricsResult { Found = false };
            }

            string cacheKey = $"{trackName.Trim().ToLowerInvariant()}|{artistName.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            try
            {
                // Request from LrcLib API
                string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}";
                if (!string.IsNullOrWhiteSpace(albumName))
                {
                    url += $"&album_name={Uri.EscapeDataString(albumName)}";
                }
                if (durationSeconds > 0)
                {
                    url += $"&duration={Math.Round(durationSeconds)}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("SpotifyNowPlayingOverlay/1.0");

                var response = await _httpClient.SendAsync(request);
                LrcLibResponse? dto = null;

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    dto = JsonSerializer.Deserialize<LrcLibResponse>(json);
                }
                else
                {
                    // Fallback to search endpoint
                    string searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString($"{trackName} {artistName}")}";
                    var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    searchRequest.Headers.UserAgent.ParseAdd("SpotifyNowPlayingOverlay/1.0");

                    var searchResponse = await _httpClient.SendAsync(searchRequest);
                    if (searchResponse.IsSuccessStatusCode)
                    {
                        string searchJson = await searchResponse.Content.ReadAsStringAsync();
                        var searchResults = JsonSerializer.Deserialize<List<LrcLibResponse>>(searchJson);
                        dto = searchResults?.FirstOrDefault();
                    }
                }

                var result = ProcessLrcLibResponse(dto);
                _cache[cacheKey] = result;
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch lyrics for {Track} by {Artist}", trackName, artistName);
                var emptyResult = new LyricsResult { Found = false };
                _cache[cacheKey] = emptyResult;
                return emptyResult;
            }
        }

        private static LyricsResult ProcessLrcLibResponse(LrcLibResponse? dto)
        {
            if (dto == null)
            {
                return new LyricsResult { Found = false };
            }

            var result = new LyricsResult
            {
                Found = true,
                PlainLyrics = dto.PlainLyrics ?? string.Empty
            };

            if (!string.IsNullOrWhiteSpace(dto.SyncedLyrics))
            {
                var lines = ParseLrc(dto.SyncedLyrics);
                if (lines.Count > 0)
                {
                    result.IsSynced = true;
                    result.SyncedLines = lines;
                }
            }

            return result;
        }

        private static List<LyricsLine> ParseLrc(string lrcContent)
        {
            var result = new List<LyricsLine>();
            var rawLines = lrcContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in rawLines)
            {
                var match = LrcTimeRegex.Match(rawLine.Trim());
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int minutes) &&
                        double.TryParse(match.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds))
                    {
                        TimeSpan timestamp = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                        string text = match.Groups[3].Value.Trim();
                        result.Add(new LyricsLine(timestamp, text));
                    }
                }
            }

            return result.OrderBy(l => l.Timestamp).ToList();
        }

        private class LrcLibResponse
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("trackName")]
            public string? TrackName { get; set; }

            [JsonPropertyName("artistName")]
            public string? ArtistName { get; set; }

            [JsonPropertyName("plainLyrics")]
            public string? PlainLyrics { get; set; }

            [JsonPropertyName("syncedLyrics")]
            public string? SyncedLyrics { get; set; }
        }
    }
}
