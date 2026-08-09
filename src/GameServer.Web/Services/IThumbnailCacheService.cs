namespace GameServer.Web.Services;

public interface IThumbnailCacheService
{
    Task<string?> GetCachedThumbnailUrlAsync(string? sourceUrl, CancellationToken cancellationToken = default);
}
