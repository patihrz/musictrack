using System;
using System.Threading;
using System.Threading.Tasks;
using SpotifyNowPlayingOverlay.Models;
using Serilog;

namespace SpotifyNowPlayingOverlay.Services
{
    public class SmartPollingService : ISmartPollingService
    {
        private readonly ISpotifyService _spotifyService;

        public SmartPollingService(ISpotifyService spotifyService)
        {
            _spotifyService = spotifyService;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_cts != null) return;
                _cts = new CancellationTokenSource();
            }

            _ = PollingLoopAsync(_cts.Token);
            Log.Information("Smart Polling Service started");
        }

        public void Stop()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
            Log.Information("Smart Polling Service stopped");
        }

        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private int _consecutiveFailures;

        public bool IsPolling => _cts != null;

        private async Task PollingLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TimeSpan delay = TimeSpan.FromSeconds(1);

                if (!_spotifyService.IsLoggedIn)
                {
                    delay = TimeSpan.FromSeconds(5);
                }
                else
                {
                    try
                    {
                        var state = await _spotifyService.UpdateCurrentPlaybackAsync();

                        if (state.Status == PlaybackStatus.Offline)
                        {
                            _consecutiveFailures++;
                            int backoffSecs = _consecutiveFailures switch
                            {
                                1 => 5,
                                2 => 10,
                                _ => 20
                            };
                            delay = TimeSpan.FromSeconds(backoffSecs);
                            Log.Warning("Spotify API unreachable (Offline). Retry backoff {Seconds}s. Failure count: {Count}", backoffSecs, _consecutiveFailures);
                        }
                        else
                        {
                            _consecutiveFailures = 0; // Reset failures on successful response

                            if (state.Status == PlaybackStatus.Playing)
                            {
                                delay = TimeSpan.FromSeconds(1);
                            }
                            else if (state.Status == PlaybackStatus.Paused)
                            {
                                delay = TimeSpan.FromSeconds(3); // Paused: 3s
                            }
                            else if (state.Status == PlaybackStatus.NoMusic)
                            {
                                delay = TimeSpan.FromSeconds(5); // No active device: 5s
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _consecutiveFailures++;
                        int backoffSecs = _consecutiveFailures switch
                        {
                            1 => 5,
                            2 => 10,
                            _ => 20
                        };
                        Log.Error(ex, "Error in polling loop. Retry backoff {Seconds}s. Failure count: {Count}", backoffSecs, _consecutiveFailures);
                        delay = TimeSpan.FromSeconds(backoffSecs);
                    }
                }

                try
                {
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
