namespace GameServer.Web.Services;

public sealed class ThumbnailCacheOptions
{
    public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "GameServer.Web", "thumbnail-cache");

    public string RequestPath { get; set; } = "/thumbnail-cache";
}
