using System.Threading.Tasks;

namespace SpotifyNowPlayingOverlay.Services
{
    public interface IArtworkCacheManager
    {
        string CacheDirectory { get; }
        void StartCleanupWorker();
        void StopCleanupWorker();
        Task RunCleanupAsync();
    }
}
