using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Route("api/v2/gameservers")]
[Authorize]
public sealed class GameServersController(
    GameServerQueryService queryService,
    GameServerCommandService commandService,
    ILogger<GameServersController> logger,
    IServerAuthorizationService? serverAuthorizationService = null,
    Repositories.V2.IGameServerResourceUtilizationRepository? resourceUtilizationRepository = null,
    IGameServerResourceCollector? resourceCollector = null,
    Interfaces.IServerResourceMonitor? resourceMonitor = null,
    Repositories.V2.IGroupRepository? groupRepository = null,
    Interfaces.IServiceOperations? serviceOperations = null,
    Services.NodeAgentClient? nodeAgentClient = null,
    Interfaces.INodeAgentDiscovery? nodeAgentDiscovery = null) : ControllerBase
{
    /// <summary>
    /// Gets the V2 GameServer list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IEnumerable<GameServerListItemDto>))]
    public async Task<ActionResult<IReadOnlyList<GameServerListItemDto>>> GetAll([FromQuery] bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting V2 game servers (IncludeDeleted={IncludeDeleted})", includeDeleted);
        var servers = await queryService.GetListAsync(includeDeleted, cancellationToken);
        if (serverAuthorizationService is not null)
        {
            var accessibleServerIds = await serverAuthorizationService.GetAccessibleServerIdsAsync(User, cancellationToken);
            if (accessibleServerIds is not null)
            {
                var accessibleSet = accessibleServerIds.ToHashSet();
                servers = servers.Where(s => accessibleSet.Contains(s.ServerId)).ToList();
            }
        }

        return Ok(servers);
    }

    /// <summary>
    /// Gets the V2 GameServer detail payload for a specific server id.
    /// </summary>
    [HttpGet("{serverId}")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> GetByServerId(string serverId, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanViewServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        var server = await queryService.GetByServerIdAsync(serverId, User, cancellationToken);
        if (server is null)
        {
            logger.LogDebug("V2 game server '{ServerId}' was not found", serverId);
            return NotFound();
        }

        return Ok(server);
    }

    /// <summary>
    /// Validates a V2 GameServer request.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(200, Type = typeof(GameServerValidationResultDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerValidationResultDto>> Validate([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await commandService.ValidateAsync(request, cancellationToken);
            return Ok(validation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Produces a dry-run preview of the Swarm service that would be created for a request.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(200, Type = typeof(GameServerDeploymentPreviewDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerDeploymentPreviewDto>> Preview([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var preview = await commandService.PreviewAsync(request, cancellationToken);
            return Ok(preview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Checks whether the supplied published ports are available for the given server.
    /// </summary>
    [HttpPost("ports/availability")]
    [ProducesResponseType(200, Type = typeof(GameServerPortAvailabilityResultDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerPortAvailabilityResultDto>> CheckPortAvailability([FromBody] GameServerPortAvailabilityRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await commandService.CheckPortAvailabilityAsync(request, cancellationToken);
            return Ok(availability);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates a V2 GameServer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(201, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerDetailDto>> Create([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = int.TryParse(currentUserIdClaim, out var parsedId) ? parsedId : null;

            var created = await commandService.CreateAsync(request, currentUserId, User, cancellationToken);

            if (request.InitialGroupId.HasValue && groupRepository is not null)
            {
                if (!User.IsInRole("Admin"))
                {
                    if (currentUserId is null)
                    {
                        return Forbid();
                    }

                    var userGroupIds = await groupRepository.GetUserGroupIdsAsync(currentUserId.Value, cancellationToken).ConfigureAwait(false);
                    if (!userGroupIds.Contains(request.InitialGroupId.Value))
                    {
                        return Forbid();
                    }
                }

                var accessLevel = string.Equals(request.InitialGroupAccessLevel, "View", StringComparison.OrdinalIgnoreCase) ? "View" : "Edit";
                await groupRepository.AddOrUpdateServerAccessAsync(
                    request.InitialGroupId.Value,
                    created.ServerId,
                    accessLevel,
                    cancellationToken).ConfigureAwait(false);
            }

            return CreatedAtAction(nameof(GetByServerId), new { serverId = created.ServerId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Updates a V2 GameServer.
    /// </summary>
    [HttpPut("{serverId}")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Update(string serverId, [FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var updated = await commandService.UpdateAsync(serverId, request, User, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Starts the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/start")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Start(string serverId, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var server = await commandService.StartAsync(serverId, User, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Stops the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/stop")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Stop(string serverId, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var server = await commandService.StopAsync(serverId, User, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Restarts the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/restart")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Restart(string serverId, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var server = await commandService.RestartAsync(serverId, User, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Redeploys and updates the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/redeploy")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Redeploy(string serverId, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var server = await commandService.RedeployAsync(serverId, User, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a V2 GameServer.
    /// </summary>
    [HttpDelete("{serverId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string serverId, [FromQuery] bool softDelete = true, CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanEditServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        try
        {
            await commandService.DeleteAsync(serverId, softDelete, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Gets the historical resource utilization records for a V2 GameServer.
    /// </summary>
    [HttpGet("{serverId}/resources/history")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<GameServerResourceHistoryDto>))]
    [ProducesResponseType(403)]
    public async Task<ActionResult<IReadOnlyList<GameServerResourceHistoryDto>>> GetResourceHistory(
        string serverId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanViewServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        if (resourceUtilizationRepository is null)
        {
            return Ok(Array.Empty<GameServerResourceHistoryDto>());
        }

        var records = await resourceUtilizationRepository.GetHistoryAsync(serverId, from, to, limit, cancellationToken);
        var dtos = records.Select(r => new GameServerResourceHistoryDto
        {
            Id = r.Id,
            ServerId = r.ServerId,
            Timestamp = r.Timestamp,
            CpuUsagePercent = r.CpuUsagePercent,
            MemoryUsageBytes = r.MemoryUsageBytes,
            MemoryLimitBytes = r.MemoryLimitBytes,
            MemoryUsagePercent = r.MemoryUsagePercent,
            NetworkRxBytes = r.NetworkRxBytes,
            NetworkTxBytes = r.NetworkTxBytes,
            BlockReadBytes = r.BlockReadBytes,
            BlockWriteBytes = r.BlockWriteBytes,
            DesiredReplicas = r.DesiredReplicas,
            RunningReplicas = r.RunningReplicas,
            ContainerId = r.ContainerId
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets the latest resource utilization snapshot for a V2 GameServer.
    /// </summary>
    [HttpGet("{serverId}/resources/latest")]
    [ProducesResponseType(200, Type = typeof(Models.ServerResourceUsage))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Models.ServerResourceUsage>> GetLatestResource(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanViewServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        // Try in-memory cached snapshot first
        var cached = resourceCollector?.GetCachedUsage(serverId);
        if (cached != null)
        {
            return Ok(cached);
        }

        // Fall back to on-demand snapshot
        if (resourceMonitor is not null)
        {
            var snapshot = await resourceMonitor.GetSnapshotAsync(serverId, cancellationToken);
            if (snapshot != null)
            {
                return Ok(snapshot);
            }
        }

        return NotFound();
    }

    /// <summary>
    /// Gets the per-group access for a specific server, limited to the groups the current user belongs to.
    /// Only the server's creator (or an admin) may call this endpoint.
    /// </summary>
    [HttpGet("{serverId}/group-access")]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<ServerGroupAccessRowDto>))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<ServerGroupAccessRowDto>>> GetGroupAccess(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        if (groupRepository is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var server = await queryService.GetByServerIdAsync(serverId, User, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(currentUserIdClaim, out var currentUserId))
        {
            return Forbid();
        }

        var isAdmin = User.IsInRole("Admin");
        var isCreator = server.CreatedByUserId.HasValue && server.CreatedByUserId.Value == currentUserId;
        if (!isAdmin && !isCreator)
        {
            return Forbid();
        }

        var userGroupIds = await groupRepository.GetUserGroupIdsAsync(currentUserId, cancellationToken).ConfigureAwait(false);
        if (userGroupIds.Count == 0)
        {
            return Ok(Array.Empty<ServerGroupAccessRowDto>());
        }

        var rows = new List<ServerGroupAccessRowDto>(userGroupIds.Count);
        foreach (var groupId in userGroupIds)
        {
            var group = await groupRepository.GetByIdAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                continue;
            }

            var existing = group.ServerGroups.FirstOrDefault(sg => sg.GameServer.ServerId == serverId);
            rows.Add(new ServerGroupAccessRowDto(
                GroupId: group.Id,
                GroupName: group.Name,
                Description: group.Description,
                AccessLevel: existing?.AccessLevel ?? "None"));
        }

        return Ok(rows);
    }

    /// <summary>
    /// Sets the per-group access for a specific server for a subset of the current user's groups.
    /// Only the server's creator (or an admin) may call this endpoint.
    /// </summary>
    [HttpPut("{serverId}/group-access")]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<ServerGroupAccessRowDto>))]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<ServerGroupAccessRowDto>>> SetGroupAccess(
        string serverId,
        [FromBody] SetServerGroupAccessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (groupRepository is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        if (request is null || request.Groups is null)
        {
            return BadRequest(new { error = "Group assignments are required." });
        }

        var server = await queryService.GetByServerIdAsync(serverId, User, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(currentUserIdClaim, out var currentUserId))
        {
            return Forbid();
        }

        var isAdmin = User.IsInRole("Admin");
        var isCreator = server.CreatedByUserId.HasValue && server.CreatedByUserId.Value == currentUserId;
        if (!isAdmin && !isCreator)
        {
            return Forbid();
        }

        // Defense-in-depth: only allow modifying groups the caller is a member of.
        var userGroupIds = (await groupRepository.GetUserGroupIdsAsync(currentUserId, cancellationToken).ConfigureAwait(false)).ToHashSet();

        foreach (var assignment in request.Groups)
        {
            if (!userGroupIds.Contains(assignment.GroupId))
            {
                return Forbid();
            }

            var level = assignment.AccessLevel?.Trim() ?? "None";
            if (string.Equals(level, "None", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(level))
            {
                await groupRepository.RemoveServerAccessAsync(assignment.GroupId, serverId, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(level, "View", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(level, "Edit", StringComparison.OrdinalIgnoreCase))
            {
                await groupRepository.AddOrUpdateServerAccessAsync(assignment.GroupId, serverId, level, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return BadRequest(new { error = $"Invalid access level '{assignment.AccessLevel}'." });
            }
        }

        logger.LogInformation("User {UserId} updated group access for server {ServerId} ({Count} group(s)).",
            currentUserId, serverId, request.Groups.Count);

        // Return refreshed rows so the caller can update its UI.
        var refreshed = new List<ServerGroupAccessRowDto>();
        foreach (var groupId in userGroupIds)
        {
            var group = await groupRepository.GetByIdAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (group is null)
            {
                continue;
            }

            var existing = group.ServerGroups.FirstOrDefault(sg => sg.GameServer.ServerId == serverId);
            refreshed.Add(new ServerGroupAccessRowDto(
                GroupId: group.Id,
                GroupName: group.Name,
                Description: group.Description,
                AccessLevel: existing?.AccessLevel ?? "None"));
        }

        return Ok(refreshed);
    }

    /// <summary>
    /// Gets all current and historical instances/tasks for a game server.
    /// </summary>
    [HttpGet("{serverId}/instances")]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<GameServerInstanceInfoDto>))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<GameServerInstanceInfoDto>>> GetInstances(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanViewServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        var server = await queryService.GetByServerIdAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        if (serviceOperations is null)
        {
            return Ok(Array.Empty<GameServerInstanceInfoDto>());
        }

        try
        {
            var services = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken);
            var service = services.FirstOrDefault();
            if (service == null)
            {
                return Ok(Array.Empty<GameServerInstanceInfoDto>());
            }

            var tasks = await serviceOperations.ListTasksAsync(
                new Docker.DotNet.Models.TasksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["service"] = new Dictionary<string, bool> { [service.ID] = true }
                    }
                },
                cancellationToken);

            var list = tasks.Select(t =>
            {
                var containerId = t.Status?.ContainerStatus?.ContainerID;
                var isRunning = t.Status?.State == Docker.DotNet.Models.TaskState.Running;
                return new GameServerInstanceInfoDto
                {
                    InstanceId = !string.IsNullOrWhiteSpace(containerId) ? containerId : t.ID,
                    TaskId = t.ID,
                    ContainerId = containerId,
                    NodeId = t.NodeID,
                    State = t.Status?.State.ToString() ?? "Unknown",
                    DesiredState = t.DesiredState.ToString(),
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    IsCurrent = isRunning,
                    Slot = (int?)t.Slot,
                    Error = t.Status?.Err
                };
            })
            .OrderByDescending(i => i.IsCurrent)
            .ThenByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            .ToList();

            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve instances for server {ServerId}", serverId);
            return Ok(Array.Empty<GameServerInstanceInfoDto>());
        }
    }

    /// <summary>
    /// Gets static logs for a game server instance (current or past).
    /// </summary>
    [HttpGet("{serverId}/logs")]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<string>))]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetLogs(
        string serverId,
        [FromQuery] string? instanceId = null,
        [FromQuery] int tail = 2000,
        CancellationToken cancellationToken = default)
    {
        if (serverAuthorizationService is not null && !await serverAuthorizationService.CanViewServerAsync(User, serverId, cancellationToken))
        {
            return Forbid();
        }

        var server = await queryService.GetByServerIdAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        if (nodeAgentDiscovery is null || nodeAgentClient is null)
        {
            return Ok(Array.Empty<string>());
        }

        try
        {
            var targetContainerId = instanceId;

            // If instanceId is a task ID or null, resolve to container ID
            if (string.IsNullOrWhiteSpace(targetContainerId) && serviceOperations != null)
            {
                var services = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken);
                var service = services.FirstOrDefault();
                if (service != null)
                {
                    var tasks = await serviceOperations.ListTasksAsync(
                        new Docker.DotNet.Models.TasksListParameters
                        {
                            Filters = new Dictionary<string, IDictionary<string, bool>>
                            {
                                ["service"] = new Dictionary<string, bool> { [service.ID] = true }
                            }
                        },
                        cancellationToken);

                    var activeOrLast = tasks.OrderByDescending(t => t.Status?.State == Docker.DotNet.Models.TaskState.Running)
                        .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                        .FirstOrDefault();

                    targetContainerId = activeOrLast?.Status?.ContainerStatus?.ContainerID ?? activeOrLast?.ID;
                }
            }
            else if (serviceOperations != null && targetContainerId != null && targetContainerId.Length > 20 && !targetContainerId.All(char.IsLetterOrDigit))
            {
                // Likely a task ID with hyphens or non-hex
                var tasks = await serviceOperations.ListTasksAsync(
                    new Docker.DotNet.Models.TasksListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["id"] = new Dictionary<string, bool> { [targetContainerId] = true }
                        }
                    },
                    cancellationToken);

                var task = tasks.FirstOrDefault();
                if (task?.Status?.ContainerStatus?.ContainerID != null)
                {
                    targetContainerId = task.Status.ContainerStatus.ContainerID;
                }
            }

            if (string.IsNullOrWhiteSpace(targetContainerId))
            {
                // Fallback to resource monitor or agent lookup
                var agentForServer = await nodeAgentDiscovery.GetAgentForServerAsync(serverId);
                if (agentForServer != null)
                {
                    targetContainerId = await Hubs.ServerLogsHub.ResolveContainerIdAsync(agentForServer, serverId, cancellationToken);
                }
            }

            if (string.IsNullOrWhiteSpace(targetContainerId))
            {
                return Ok(new[] { "No container or task instance found for this server." });
            }

            var logs = await nodeAgentDiscovery.GetContainerLogsAsync(targetContainerId, tail);
            if (logs == null || logs.Count == 0)
            {
                return Ok("No log entries available for this container instance.");
            }

            return Ok(string.Join("\n", logs));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for server {ServerId}, instance {InstanceId}", serverId, instanceId);
            return StatusCode(500, $"Error retrieving logs: {ex.Message}");
        }
    }
}
