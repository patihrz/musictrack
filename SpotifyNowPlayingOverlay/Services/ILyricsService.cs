using System.Threading.Tasks;
using SpotifyNowPlayingOverlay.Models;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface ILyricsService
    {
        Task<LyricsResult> GetLyricsAsync(string trackName, string artistName, string albumName, double durationSeconds);
    }
}
