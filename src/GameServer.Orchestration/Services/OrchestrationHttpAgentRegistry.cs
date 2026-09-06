using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace GameServer.Orchestration.Services;

/// <summary>
/// HTTP client implementation of IAgentRegistry.
/// Reads agent registry state from the GameServer.Orchestration.Host REST API.
/// Mutations (RegisterAgent, UpdateAgentContainers, MarkAgentDisconnected) are
/// performed by the Orchestration Host's AgentRegistrationHub directly — this
/// client provides read-only access for consumers hosted outside Orchestration.Host.
/// </summary>
public class OrchestrationHttpAgentRegistry(
    HttpClient httpClient,
    ILogger<OrchestrationHttpAgentRegistry> logger) : IAgentRegistry
{
    private List<NodeAgentEndpoint> FetchAgents(string path)
    {
        try
        {
            return httpClient.GetFromJsonAsync<List<NodeAgentEndpoint>>(path).GetAwaiter().GetResult()
                   ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch agents from Orchestration service at {Path}", path);
            return [];
        }
    }

    private NodeAgentEndpoint? FetchAgent(string path)
    {
        try
        {
            var response = httpClient.GetAsync(path).GetAwaiter().GetResult();
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return response.Content.ReadFromJsonAsync<NodeAgentEndpoint>().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch agent from Orchestration service at {Path}", path);
            return null;
        }
    }

    public List<NodeAgentEndpoint> GetAllAgents()         => FetchAgents("api/agents");
    public List<NodeAgentEndpoint> GetHealthyAgents()     => FetchAgents("api/agents/healthy");
    public List<NodeAgentEndpoint> GetManagerAgents()     => FetchAgents("api/agents/managers");
    public NodeAgentEndpoint?      GetHealthyManagerAgent() => FetchAgent("api/agents/manager/healthy");

    public NodeAgentEndpoint? GetAgentForContainer(string containerId)
        => FetchAgent($"api/agents/by-container/{Uri.EscapeDataString(containerId)}");

    public NodeAgentEndpoint? GetAgentByNodeId(string nodeId)
        => FetchAgent($"api/agents/by-node/{Uri.EscapeDataString(nodeId)}");

    public NodeAgentEndpoint? GetAgentByConnectionId(string connectionId)
        => FetchAgent($"api/agents/by-connection/{Uri.EscapeDataString(connectionId)}");

    // Write operations are owned by AgentRegistrationHub in GameServer.Orchestration.Host.
    // Consumers using the HTTP registry should never call these — implemented as
    // no-ops so the IAgentRegistry contract is satisfied.

    public void RegisterAgent(AgentRegistrationInfo info, string connectionId)
        => logger.LogWarning("RegisterAgent called on OrchestrationHttpAgentRegistry — this is a no-op. Agents should register with Orchestration.Host.");

    public void UpdateAgentContainers(string connectionId, List<string> containerIds)
        => logger.LogTrace("UpdateAgentContainers called on OrchestrationHttpAgentRegistry — no-op.");

    public void MarkAgentDisconnected(string connectionId)
        => logger.LogTrace("MarkAgentDisconnected called on OrchestrationHttpAgentRegistry — no-op.");
}
