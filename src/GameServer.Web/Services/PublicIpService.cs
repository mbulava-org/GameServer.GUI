using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameServer.Web.Services;

public class PublicIpService : IPublicIpService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PublicIpService> _logger;
    private string? _cachedIp;
    private DateTime _cacheExpiration = DateTime.MinValue;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly string[] IpEndpoints =
    [
        "https://api.myip.com",
        "https://api.ipify.org",
        "https://icanhazip.com",
        "https://ifconfig.me/ip",
        "https://checkip.amazonaws.com"
    ];

    public PublicIpService(IHttpClientFactory httpClientFactory, ILogger<PublicIpService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetPublicIpAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedIp) && DateTime.UtcNow < _cacheExpiration)
        {
            return _cachedIp;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedIp) && DateTime.UtcNow < _cacheExpiration)
            {
                return _cachedIp;
            }

            var client = _httpClientFactory.CreateClient("PublicIpDiscovery");
            client.Timeout = TimeSpan.FromSeconds(3);

            foreach (var endpoint in IpEndpoints)
            {
                try
                {
                    using var response = await client.GetAsync(endpoint, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                        var candidateIp = ExtractIpAddress(endpoint, content);
                        if (!string.IsNullOrWhiteSpace(candidateIp) && IPAddress.TryParse(candidateIp, out _))
                        {
                            _cachedIp = candidateIp;
                            _cacheExpiration = DateTime.UtcNow.AddHours(1);
                            _logger.LogInformation("Discovered public internet IP: {PublicIp} via {Endpoint}", candidateIp, endpoint);
                            return _cachedIp;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve public IP from {Endpoint}", endpoint);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return _cachedIp ?? "127.0.0.1";
    }

    private static string? ExtractIpAddress(string endpoint, string content)
    {
        if (endpoint.Contains("myip.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("ip", out var ipProp))
                {
                    return ipProp.GetString()?.Trim();
                }
            }
            catch
            {
                return null;
            }
        }

        return content;
    }
}
