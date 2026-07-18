using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        event System.EventHandler? SettingsChanged;
        void LoadSettings();
        void SaveSettings();
        string GetRefreshToken();
        void SetRefreshToken(string refreshToken);
    }
}
