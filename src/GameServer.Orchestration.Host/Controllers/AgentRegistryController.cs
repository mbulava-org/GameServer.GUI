using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Orchestration.Host.Controllers;

/// <summary>
/// REST API exposing IAgentRegistry reads so GameServer.API can query
/// the agent registry over HTTP without an in-process dependency on the
/// Orchestration module.
/// </summary>
[ApiController]
[Route("api/agents")]
public class AgentRegistryController(IAgentRegistry agentRegistry) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<NodeAgentEndpoint>> GetAll()
        => Ok(agentRegistry.GetAllAgents());

    [HttpGet("healthy")]
    public ActionResult<List<NodeAgentEndpoint>> GetHealthy()
        => Ok(agentRegistry.GetHealthyAgents());

    [HttpGet("managers")]
    public ActionResult<List<NodeAgentEndpoint>> GetManagers()
        => Ok(agentRegistry.GetManagerAgents());

    [HttpGet("manager/healthy")]
    public ActionResult<NodeAgentEndpoint?> GetHealthyManager()
    {
        var agent = agentRegistry.GetHealthyManagerAgent();
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpGet("by-container/{containerId}")]
    public ActionResult<NodeAgentEndpoint?> GetByContainer(string containerId)
    {
        var agent = agentRegistry.GetAgentForContainer(containerId);
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpGet("by-node/{nodeId}")]
    public ActionResult<NodeAgentEndpoint?> GetByNode(string nodeId)
    {
        var agent = agentRegistry.GetAgentByNodeId(nodeId);
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpGet("by-connection/{connectionId}")]
    public ActionResult<NodeAgentEndpoint?> GetByConnection(string connectionId)
    {
        var agent = agentRegistry.GetAgentByConnectionId(connectionId);
        return agent is null ? NotFound() : Ok(agent);
    }
}
