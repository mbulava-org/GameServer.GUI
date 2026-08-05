using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Services;

public sealed class ThumbnailCacheService(
    IHttpClientFactory httpClientFactory,
    IOptions<ThumbnailCacheOptions> options,
    ILogger<ThumbnailCacheService> logger) : IThumbnailCacheService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadLocks = new(StringComparer.Ordinal);

    private readonly ThumbnailCacheOptions cacheOptions = options.Value;

    public async Task<string?> GetCachedThumbnailUrlAsync(string? sourceUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return sourceUrl;
        }

        Directory.CreateDirectory(cacheOptions.CacheDirectory);

        var hash = ComputeHash(sourceUrl);
        var existingFilePath = Directory
            .EnumerateFiles(cacheOptions.CacheDirectory, $"{hash}.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (existingFilePath is not null)
        {
            return BuildCachedUrl(Path.GetFileName(existingFilePath));
        }

        var downloadLock = DownloadLocks.GetOrAdd(hash, static _ => new SemaphoreSlim(1, 1));
        await downloadLock.WaitAsync(cancellationToken);

        try
        {
            existingFilePath = Directory
                .EnumerateFiles(cacheOptions.CacheDirectory, $"{hash}.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (existingFilePath is not null)
            {
                return BuildCachedUrl(Path.GetFileName(existingFilePath));
            }

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var extension = ResolveExtension(response.Content.Headers.ContentType?.MediaType, uri);
            var fileName = $"{hash}{extension}";
            var filePath = Path.Combine(cacheOptions.CacheDirectory, fileName);
            var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

            await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destinationStream = File.Create(tempFilePath))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            if (File.Exists(filePath))
            {
                File.Delete(tempFilePath);
            }
            else
            {
                File.Move(tempFilePath, filePath);
            }

            return BuildCachedUrl(fileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache thumbnail from {ThumbnailSourceUrl}", sourceUrl);
            return sourceUrl;
        }
        finally
        {
            downloadLock.Release();
        }
    }

    private string BuildCachedUrl(string fileName)
    {
        return $"{cacheOptions.RequestPath.TrimEnd('/')}/{Uri.EscapeDataString(fileName)}";
    }

    private static string ComputeHash(string sourceUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceUrl));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ResolveExtension(string? mediaType, Uri uri)
    {
        var extensionFromContentType = mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/avif" => ".avif",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(extensionFromContentType))
        {
            return extensionFromContentType;
        }

        var uriExtension = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(uriExtension) && uriExtension.Length <= 8)
        {
            return uriExtension.ToLowerInvariant();
        }

        return ".jpg";
    }
}
