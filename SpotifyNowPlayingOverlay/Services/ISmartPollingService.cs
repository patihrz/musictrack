using System;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface ISmartPollingService
    {
        void Start();
        void Stop();
        bool IsPolling { get; }
    }
}
