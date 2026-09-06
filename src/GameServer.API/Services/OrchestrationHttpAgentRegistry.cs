using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using GameServer.API.Configurations;

namespace GameServer.API.Services;

/// <summary>
/// HTTP client implementation of IAgentRegistry.
/// Reads agent registry state from the GameServer.Orchestration.Host REST API.
/// Mutations (RegisterAgent, UpdateAgentContainers, MarkAgentDisconnected) are
/// performed by the Orchestration Host's AgentRegistrationHub directly — this
/// client provides read-only access for the API layer.
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

    // ── IAgentRegistry reads (delegated to Orchestration.Host) ────────────────

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

    // ── Write operations: no-op in API — owned by Orchestration.Host hub ─────
    // These methods are called by AgentRegistrationHub (which lives in Orchestration.Host).
    // The API no longer hosts that hub, so these should never be called. They are implemented
    // as no-ops to satisfy the interface contract and for test compatibility.

    public void RegisterAgent(AgentRegistrationInfo info, string connectionId)
        => logger.LogWarning("RegisterAgent called on OrchestrationHttpAgentRegistry — this is a no-op. Agents should register with Orchestration.Host.");

    public void UpdateAgentContainers(string connectionId, List<string> containerIds)
        => logger.LogTrace("UpdateAgentContainers called on OrchestrationHttpAgentRegistry — no-op.");

    public void MarkAgentDisconnected(string connectionId)
        => logger.LogTrace("MarkAgentDisconnected called on OrchestrationHttpAgentRegistry — no-op.");
}
