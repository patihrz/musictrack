using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyNowPlayingOverlay.Models;
using SpotifyNowPlayingOverlay.Utils;
using Serilog;

namespace SpotifyNowPlayingOverlay.Services
{
    public class SpotifyService : ISpotifyService
    {
        private readonly ISettingsService _settingsService;
        private readonly IArtworkCacheManager _cacheManager;
        private readonly HttpClient _httpClient;

        private string _accessToken = string.Empty;
        private DateTime _tokenExpirationTime = DateTime.MinValue;
        private CurrentlyPlayingState _currentState = new();
        private HttpListener? _httpListener;
        private string? _currentCodeVerifier;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private SpotifyClient? _spotifyClient;
        private DateTime _rateLimitResetTime = DateTime.MinValue;

        public CurrentlyPlayingState CurrentState => _currentState;
        public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken) || !string.IsNullOrEmpty(_settingsService.GetRefreshToken());

        public event EventHandler<CurrentlyPlayingState>? PlaybackUpdated;

        public SpotifyService(
            ISettingsService settingsService,
            IArtworkCacheManager cacheManager,
            HttpClient httpClient)
        {
            _settingsService = settingsService;
            _cacheManager = cacheManager;
            _httpClient = httpClient;
        }

        public async Task<bool> TryAutoLoginAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                string refreshToken = _settingsService.GetRefreshToken();
                if (string.IsNullOrEmpty(refreshToken))
                {
                    Log.Information("No refresh token cached. Auto login skipped.");
                    return false;
                }

                Log.Information("Attempting auto login with cached refresh token.");
                bool success = await RefreshTokenAsync(refreshToken);
                if (success)
                {
                    Log.Information("Auto login successful.");
                    await UpdateCurrentPlaybackAsync();
                }
                else
                {
                    Log.Warning("Auto login failed due to token refresh error.");
                    HandleAuthFailure();
                }
                return success;
            }
            finally
            {
                _authLock.Release();
            }
        }

        public async Task StartLoginFlowAsync()
        {
            await _authLock.WaitAsync();
            try
            {
                string clientId = _settingsService.CurrentSettings.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    Log.Error("Login aborted: Spotify Client ID is empty.");
                    throw new InvalidOperationException("Spotify Client ID is not configured in Settings.");
                }

                Log.Information("Initiating OAuth PKCE login flow.");
                _currentCodeVerifier = OAuthHelper.GenerateCodeVerifier();
                string codeChallenge = OAuthHelper.GenerateCodeChallenge(_currentCodeVerifier);

                _httpListener?.Stop();
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://127.0.0.1:8888/callback/");
                _httpListener.Start();

                string redirectUri = Uri.EscapeDataString("http://127.0.0.1:8888/callback");
                string scope = Uri.EscapeDataString("user-read-currently-playing user-read-playback-state");
                string url = $"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=code&redirect_uri={redirectUri}&code_challenge_method=S256&code_challenge={codeChallenge}&scope={scope}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                Log.Information("OAuth authorize page opened in browser: {Url}", url);

                _ = Task.Run(() => ListenForAuthCallbackAsync());
            }
            finally
            {
                _authLock.Release();
            }
        }

        private async Task ListenForAuthCallbackAsync()
        {
            if (_httpListener == null) return;

            try
            {
                var context = await _httpListener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                string? code = request.QueryString["code"];
                string? error = request.QueryString["error"];

                string responseString;
                if (!string.IsNullOrEmpty(code))
                {
                    responseString = "<html><body style='font-family:sans-serif; text-align:center; padding-top:50px; background-color:#121212; color:white;'><h1>Authentication Successful!</h1><p>You can now close this window and return to Spotify Now Playing Overlay.</p></body></html>";
                    Log.Information("OAuth callback received code successfully.");
                    _ = Task.Run(() => ExchangeCodeForTokenAsync(code));
                }
                else
                {
                    responseString = $"<html><body style='font-family:sans-serif; text-align:center; padding-top:50px; background-color:#121212; color:red;'><h1>Authentication Failed!</h1><p>Error: {error ?? "Unknown error"}</p></body></html>";
                    Log.Error("OAuth callback error received: {Error}", error ?? "Unknown");
                }

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "OAuth callback listener encountered an issue.");
            }
            finally
            {
                try
                {
                    _httpListener?.Stop();
                    _httpListener = null;
                }
                catch { }
            }
        }

        private async Task ExchangeCodeForTokenAsync(string code)
        {
            await _authLock.WaitAsync();
            try
            {
                string clientId = _settingsService.CurrentSettings.ClientId;
                string? codeVerifier = _currentCodeVerifier;

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(codeVerifier)) return;

                Log.Information("Exchanging auth code for tokens.");

                var oauthClient = new OAuthClient();
                var tokenResponse = await oauthClient.RequestToken(
                    new PKCETokenRequest(clientId, code, new Uri("http://127.0.0.1:8888/callback"), codeVerifier)
                );

                if (tokenResponse != null)
                {
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpirationTime = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
                    _spotifyClient = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithToken(_accessToken));

                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        _settingsService.SetRefreshToken(tokenResponse.RefreshToken);
                    }

                    Log.Information("Access token successfully acquired. Expires at {Expiration}.", _tokenExpirationTime);
                    await UpdateCurrentPlaybackAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during auth token exchange.");
                HandleAuthFailure();
            }
            finally
            {
                _authLock.Release();
            }
        }

        private async Task<bool> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                string clientId = _settingsService.CurrentSettings.ClientId;
                if (string.IsNullOrEmpty(clientId)) return false;

                Log.Information("Refreshing Spotify access token.");
                var oauthClient = new OAuthClient();
                var tokenResponse = await oauthClient.RequestToken(
                    new PKCETokenRefreshRequest(clientId, refreshToken)
                );

                if (tokenResponse != null)
                {
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpirationTime = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);
                    _spotifyClient = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithToken(_accessToken));

                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        _settingsService.SetRefreshToken(tokenResponse.RefreshToken);
                    }

                    Log.Information("Token refreshed successfully. Expires at {Expiration}.", _tokenExpirationTime);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh Spotify access token.");
            }

            return false;
        }

        private void HandleAuthFailure()
        {
            Log.Warning("Authentication failure. Clearing credentials and notifying user.");

            // Clear credentials
            _accessToken = string.Empty;
            _tokenExpirationTime = DateTime.MinValue;
            _spotifyClient = null;
            _settingsService.SetRefreshToken(string.Empty);

            // Re-open Spotify login automatically in background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000);
                    await StartLoginFlowAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error re-opening login flow after authentication failure.");
                }
            });
        }

        public void Logout()
        {
            Log.Information("Logging out of Spotify.");
            _accessToken = string.Empty;
            _tokenExpirationTime = DateTime.MinValue;
            _spotifyClient = null;
            _settingsService.SetRefreshToken(string.Empty);

            UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.NotLoggedIn });
        }

        public async Task<CurrentlyPlayingState> UpdateCurrentPlaybackAsync()
        {

            // 1.5 Rate limit check
            if (DateTime.UtcNow < _rateLimitResetTime)
            {
                return _currentState;
            }

            // 2. Auth checks
            if (string.IsNullOrEmpty(_accessToken))
            {
                string refreshToken = _settingsService.GetRefreshToken();
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    bool success = await RefreshTokenAsync(refreshToken);
                    if (!success)
                    {
                        HandleAuthFailure();
                        UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.NotLoggedIn });
                        return _currentState;
                    }
                }
                else
                {
                    UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.NotLoggedIn });
                    return _currentState;
                }
            }

            if (DateTime.UtcNow >= _tokenExpirationTime)
            {
                string refreshToken = _settingsService.GetRefreshToken();
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    bool success = await RefreshTokenAsync(refreshToken);
                    if (!success)
                    {
                        HandleAuthFailure();
                        UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.Offline, Title = "Spotify Offline" });
                        return _currentState;
                    }
                }
            }

            if (_spotifyClient == null)
            {
                _spotifyClient = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithToken(_accessToken));
            }

            try
            {
                IPlayableItem? item = null;
                bool isPlaying = false;
                int progressMs = 0;
                bool gotPlayback = false;

                try
                {
                    var playback = await _spotifyClient.Player.GetCurrentPlayback();
                    if (playback == null)
                    {
                        UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.Offline, Title = "Spotify Offline" });
                        return _currentState;
                    }

                    item = playback.Item;
                    isPlaying = playback.IsPlaying;
                    progressMs = playback.ProgressMs;
                    gotPlayback = true;
                }
                catch (NotSupportedException)
                {
                    // Fallback to GetCurrentlyPlaying
                }
                catch (APIException apiEx) when (apiEx.Response?.StatusCode == HttpStatusCode.Unauthorized || apiEx.Response?.StatusCode == (HttpStatusCode)429)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "GetCurrentPlayback failed, falling back to GetCurrentlyPlaying.");
                }

                if (!gotPlayback)
                {
                    var currentlyPlaying = await _spotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    if (currentlyPlaying == null)
                    {
                        // Do not treat a null response from GetCurrentlyPlaying() as definitive proof that no music is playing.
                        return _currentState;
                    }

                    item = currentlyPlaying.Item;
                    isPlaying = currentlyPlaying.IsPlaying;
                    progressMs = currentlyPlaying.ProgressMs ?? 0;
                }

                if (item == null)
                {
                    UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.NoMusic, Title = "No music playing" });
                    return _currentState;
                }

                if (item is FullTrack track)
                {
                    // Format Artists list
                    var artistsList = new System.Collections.Generic.List<string>();
                    foreach (var artist in track.Artists)
                    {
                        artistsList.Add(artist.Name);
                    }
                    string artists = string.Join(", ", artistsList);

                    // Album ID Artwork Caching
                    string artworkUrl = string.Empty;
                    string albumId = track.Album.Id;
                    if (track.Album.Images != null && track.Album.Images.Count > 0)
                    {
                        artworkUrl = track.Album.Images[0].Url;
                    }

                    string localArtworkPath = Path.Combine(_cacheManager.CacheDirectory, $"{albumId}.jpg");
                    bool cacheExists = File.Exists(localArtworkPath);

                    var newState = new CurrentlyPlayingState
                    {
                        Status = isPlaying ? PlaybackStatus.Playing : PlaybackStatus.Paused,
                        Title = track.Name,
                        Artist = artists,
                        AlbumArtworkUrl = artworkUrl,
                        AlbumArtworkPath = cacheExists ? localArtworkPath : string.Empty,
                        ProgressMs = progressMs,
                        DurationMs = track.DurationMs
                    };

                    UpdateState(newState);

                    if (!cacheExists && !string.IsNullOrEmpty(artworkUrl))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string downloadedPath = await GetOrCacheArtworkAsync(albumId, artworkUrl);
                                if (!string.IsNullOrEmpty(downloadedPath))
                                {
                                    // Verify that the track hasn't changed since the download started
                                    if (_currentState.Title == track.Name && _currentState.Artist == artists)
                                    {
                                        var updatedState = new CurrentlyPlayingState
                                        {
                                            Status = _currentState.Status,
                                            Title = _currentState.Title,
                                            Artist = _currentState.Artist,
                                            AlbumArtworkUrl = _currentState.AlbumArtworkUrl,
                                            AlbumArtworkPath = downloadedPath,
                                            ProgressMs = _currentState.ProgressMs,
                                            DurationMs = _currentState.DurationMs
                                        };
                                        UpdateState(updatedState);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Background artwork download failed for Album {AlbumId}", albumId);
                            }
                        });
                    }
                    else if (cacheExists)
                    {
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                File.SetLastAccessTimeUtc(localArtworkPath, DateTime.UtcNow);
                            }
                            catch { }
                        });
                    }
                }
            }
            catch (APIException apiEx) when (apiEx.Response?.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Warning("Spotify API unauthorized HTTP 401. Access token may be expired. Requesting refresh.");
                _accessToken = string.Empty; // Reset so that next tick handles refresh
            }
            catch (APIException apiEx) when (apiEx.Response?.StatusCode == (HttpStatusCode)429)
            {
                int retrySeconds = 10;
                if (apiEx.Response.Headers != null &&
                    apiEx.Response.Headers.TryGetValue("Retry-After", out string? retryStr) &&
                    int.TryParse(retryStr, out int sec))
                {
                    retrySeconds = sec;
                }
                _rateLimitResetTime = DateTime.UtcNow.AddSeconds(retrySeconds);
                Log.Error("Spotify API rate limit hit (429). Retry after {Seconds} seconds.", retrySeconds);
                UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.Offline, Title = "Rate Limited" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while updating current Spotify playback status.");
                UpdateState(new CurrentlyPlayingState { Status = PlaybackStatus.Offline, Title = "Spotify Offline" });
            }

            return _currentState;
        }

        private void UpdateState(CurrentlyPlayingState newState)
        {
            _currentState = newState;
            PlaybackUpdated?.Invoke(this, _currentState);
        }

        private async Task<string> GetOrCacheArtworkAsync(string albumId, string remoteUrl)
        {
            if (string.IsNullOrEmpty(albumId) || string.IsNullOrEmpty(remoteUrl))
                return string.Empty;

            string localPath = Path.Combine(_cacheManager.CacheDirectory, $"{albumId}.jpg");

            try
            {
                if (File.Exists(localPath))
                {
                    // Touch last access time for 90-day retention rules
                    File.SetLastAccessTimeUtc(localPath, DateTime.UtcNow);
                    return localPath;
                }

                Log.Information("Artwork not cached for Album {AlbumId}. Downloading from: {Url}", albumId, remoteUrl);
                byte[] imageBytes = await _httpClient.GetByteArrayAsync(remoteUrl);
                await File.WriteAllBytesAsync(localPath, imageBytes);

                return localPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download/cache album artwork for Album {AlbumId}", albumId);
                return remoteUrl; // Fallback to HTTP URL
            }
        }
    }
}
