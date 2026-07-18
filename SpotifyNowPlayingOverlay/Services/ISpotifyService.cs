using System;
using System.Threading.Tasks;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface ISpotifyService
    {
        CurrentlyPlayingState CurrentState { get; }
        bool IsLoggedIn { get; }
        event EventHandler<CurrentlyPlayingState>? PlaybackUpdated;
        Task<bool> TryAutoLoginAsync();
        Task StartLoginFlowAsync();
        void Logout();
        Task<CurrentlyPlayingState> UpdateCurrentPlaybackAsync();
    }
}
