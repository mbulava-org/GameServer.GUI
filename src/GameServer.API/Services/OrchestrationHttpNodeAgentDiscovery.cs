using GameServer.API.Interfaces;
using GameServer.API.Models;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace GameServer.API.Services;

/// <summary>
/// HTTP client implementation of INodeAgentDiscovery.
/// Delegates discovery and log/stats lookups to the GameServer.Orchestration.Host REST API.
/// </summary>
public class OrchestrationHttpNodeAgentDiscovery(
    HttpClient httpClient,
    ILogger<OrchestrationHttpNodeAgentDiscovery> logger) : INodeAgentDiscovery
{
    public async Task<List<NodeAgentEndpoint>> DiscoverAgentsAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<NodeAgentEndpoint>>("api/discovery/agents")
                   ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to discover agents from Orchestration service");
            return [];
        }
    }

    public async Task<NodeAgentEndpoint?> GetAgentForContainerAsync(string containerId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/discovery/container/{Uri.EscapeDataString(containerId)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NodeAgentEndpoint>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get agent for container {ContainerId} from Orchestration service", containerId);
            return null;
        }
    }

    public async Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/discovery/server/{Uri.EscapeDataString(serverId)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NodeAgentEndpoint>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get agent for server {ServerId} from Orchestration service", serverId);
            return null;
        }
    }

    public async Task<ContainerStats?> GetContainerStatsAsync(string containerId)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/discovery/stats/{Uri.EscapeDataString(containerId)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ContainerStats>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get container stats for {ContainerId} from Orchestration service", containerId);
            return null;
        }
    }

    public async IAsyncEnumerable<ContainerStats> StreamContainerStatsAsync(
        string containerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var stats = await GetContainerStatsAsync(containerId);
            if (stats != null)
            {
                yield return stats;
            }

            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    public async Task<List<string>?> GetContainerLogsAsync(string containerId, int tailLines = 1000)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/discovery/logs/{Uri.EscapeDataString(containerId)}?tailLines={tailLines}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<string>>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get container logs for {ContainerId} from Orchestration service", containerId);
            return null;
        }
    }

    public async Task<List<string>?> GetServiceLogsAsync(string serviceId, int tailLines = 1000)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/discovery/servicelogs/{Uri.EscapeDataString(serviceId)}?tailLines={tailLines}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<string>>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get service logs for {ServiceId} from Orchestration service", serviceId);
            return null;
        }
    }
}
