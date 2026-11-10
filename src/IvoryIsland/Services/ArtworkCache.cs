using System.IO;
using System.Security.Cryptography;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace IvoryIsland.Services;

/// <summary>
/// Asynchronous LRU cache for media session album artwork and video thumbnails.
/// Decodes thumbnails at bounded logical resolution (96x96), disposes WinRT streams immediately,
/// and optionally caches thumbnail blobs inside the user's selected Data/Cache folder
/// (defaulting to %LOCALAPPDATA%\IvoryIsland\Cache\Artwork) while handling missing or unplugged drives gracefully.
/// </summary>
public sealed class ArtworkCache : IDisposable
{
    private const int MaxEntries = 24;
    private const int ThumbnailDecodeSize = 96;
    private const int MaxStreamBytes = 4 * 1024 * 1024; // 4 MB safety cap

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SettingsService? _settingsService;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, BitmapImage> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lruOrder = new();
    private bool _isDisposed;

    public ArtworkCache(DispatcherQueue dispatcherQueue, SettingsService? settingsService = null)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _settingsService = settingsService;
    }

    public async Task<(string? CacheKey, BitmapImage? Image)> GetOrDecodeAsync(
        IRandomAccessStreamReference? thumbnailRef,
        string fallbackIdentityKey,
        CancellationToken cancellationToken)
    {
        if (_isDisposed || thumbnailRef is null)
            return (null, null);

        byte[] rawBytes;
        try
        {
            using IRandomAccessStreamWithContentType winrtStream = await thumbnailRef.OpenReadAsync().AsTask(cancellationToken).ConfigureAwait(false);
            if (winrtStream is null || winrtStream.Size == 0 || winrtStream.Size > MaxStreamBytes)
                return (null, null);

            using Stream readStream = winrtStream.AsStreamForRead();
            using var ms = new MemoryStream((int)winrtStream.Size);
            await readStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            rawBytes = ms.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (null, null);
        }

        if (rawBytes.Length == 0)
            return (null, null);

        string hashHex = Convert.ToHexString(SHA256.HashData(rawBytes))[..16];
        string cacheKey = $"{fallbackIdentityKey}:{hashHex}";

        lock (_syncRoot)
        {
            if (_cache.TryGetValue(cacheKey, out var cachedBitmap))
            {
                TouchKeyUnsafe(cacheKey);
                return (cacheKey, cachedBitmap);
            }
        }

        PersistThumbnailToUserCacheSafe(hashHex, rawBytes);

        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<BitmapImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested || _isDisposed)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                using var memStream = new InMemoryRandomAccessStream();
                using (Stream writeStream = memStream.AsStreamForWrite())
                {
                    await writeStream.WriteAsync(rawBytes, 0, rawBytes.Length, cancellationToken);
                    await writeStream.FlushAsync(cancellationToken);
                }
                memStream.Seek(0);

                var bitmap = new BitmapImage
                {
                    DecodePixelWidth = ThumbnailDecodeSize,
                    DecodePixelType = DecodePixelType.Logical
                };

                await bitmap.SetSourceAsync(memStream);
                tcs.TrySetResult(bitmap);
            }
            catch
            {
                tcs.TrySetResult(null);
            }
        });

        if (!enqueued)
            return (null, null);

        BitmapImage? decoded = await tcs.Task.ConfigureAwait(false);
        if (decoded is null)
            return (null, null);

        lock (_syncRoot)
        {
            if (!_cache.ContainsKey(cacheKey))
            {
                _cache[cacheKey] = decoded;
                _lruOrder.AddFirst(cacheKey);

                while (_lruOrder.Count > MaxEntries && _lruOrder.Last is not null)
                {
                    string oldestKey = _lruOrder.Last.Value;
                    _lruOrder.RemoveLast();
                    _cache.Remove(oldestKey);
                }
            }
        }

        return (cacheKey, decoded);
    }

    private void PersistThumbnailToUserCacheSafe(string hashHex, byte[] rawBytes)
    {
        if (_settingsService is null)
            return;

        try
        {
            string cacheDir = _settingsService.GetArtworkCacheDirectory();
            string filePath = Path.Combine(cacheDir, $"{hashHex}.bin");
            if (!File.Exists(filePath))
            {
                File.WriteAllBytes(filePath, rawBytes);
            }
        }
        catch
        {
        }
    }

    private void TouchKeyUnsafe(string key)
    {
        var node = _lruOrder.Find(key);
        if (node is not null)
        {
            _lruOrder.Remove(node);
            _lruOrder.AddFirst(node);
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _cache.Clear();
            _lruOrder.Clear();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        Clear();
    }
}
