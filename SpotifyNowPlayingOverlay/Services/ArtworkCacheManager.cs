using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SpotifyNowPlayingOverlay.Services
{
    public class ArtworkCacheManager : IArtworkCacheManager
    {
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();
        private const long DefaultMaxCacheSizeBytes = 500L * 1024L * 1024L; // 500 MB
        private const int DefaultMaxAgeDays = 90;

        public string CacheDirectory { get; }

        public ArtworkCacheManager()
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            CacheDirectory = Path.Combine(localApp, "MusicTrack", "ArtworkCache");

            try
            {
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create artwork cache directory: {Directory}", CacheDirectory);
            }
        }

        public void StartCleanupWorker()
        {
            lock (_lock)
            {
                if (_cts != null) return;
                _cts = new CancellationTokenSource();
            }

            _ = CleanupWorkerLoopAsync(_cts.Token);
            Log.Information("Artwork Cache Cleanup Worker started");
        }

        public void StopCleanupWorker()
        {
            lock (_lock)
            {
                if (_cts == null) return;
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            Log.Information("Artwork Cache Cleanup Worker stopped");
        }

        private async Task CleanupWorkerLoopAsync(CancellationToken token)
        {
            // 1. Run once at startup
            await RunCleanupAsync();

            // 2. Loop every 24 hours
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), token);
                    await RunCleanupAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in Artwork Cache Cleanup worker loop");
                }
            }
        }

        public Task RunCleanupAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    Log.Information("Starting Artwork Cache Cleanup...");
                    if (!Directory.Exists(CacheDirectory)) return;

                    var directoryInfo = new DirectoryInfo(CacheDirectory);
                    var files = directoryInfo.GetFiles("*.jpg")
                        .OrderBy(f => f.LastAccessTimeUtc)
                        .ToList();

                    int ageDeleted = 0;
                    long totalSize = 0;
                    var activeFiles = new System.Collections.Generic.List<FileInfo>();

                    // Step 1: Clean files older than 90 days
                    var thresholdDate = DateTime.UtcNow.AddDays(-DefaultMaxAgeDays);
                    foreach (var file in files)
                    {
                        if (file.LastAccessTimeUtc < thresholdDate)
                        {
                            try
                            {
                                file.Delete();
                                ageDeleted++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to delete expired cache file {FilePath}", file.FullName);
                            }
                        }
                        else
                        {
                            activeFiles.Add(file);
                            totalSize += file.Length;
                        }
                    }

                    if (ageDeleted > 0)
                    {
                        Log.Information("Deleted {Count} artwork cache files that have not been accessed for 90+ days", ageDeleted);
                    }

                    // Step 2: Enforce 500 MB limit
                    int sizeDeleted = 0;
                    if (totalSize > DefaultMaxCacheSizeBytes)
                    {
                        Log.Information("Cache size ({Size:F2} MB) exceeds limit ({Limit:F2} MB). Cleaning up oldest files...",
                            (double)totalSize / (1024 * 1024),
                            (double)DefaultMaxCacheSizeBytes / (1024 * 1024));

                        foreach (var file in activeFiles)
                        {
                            if (totalSize <= DefaultMaxCacheSizeBytes)
                            {
                                break;
                            }

                            try
                            {
                                long size = file.Length;
                                file.Delete();
                                totalSize -= size;
                                sizeDeleted++;
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to delete size-limit cache file {FilePath}", file.FullName);
                            }
                        }
                    }

                    if (sizeDeleted > 0)
                    {
                        Log.Information("Deleted {Count} oldest accessed artwork cache files to free space", sizeDeleted);
                    }

                    Log.Information("Artwork Cache Cleanup finished. Current cache size: {Size:F2} MB", (double)totalSize / (1024 * 1024));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Artwork Cache Cleanup encountered an error");
                }
            });
        }
    }
}
