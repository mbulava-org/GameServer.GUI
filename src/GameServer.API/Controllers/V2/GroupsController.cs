using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Route("api/v2/groups")]
[Authorize(Roles = "Admin")]
public sealed class GroupsController(
    IGroupRepository groupRepository,
    ILogger<GroupsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<GroupListItemDto>))]
    public async Task<ActionResult<IReadOnlyList<GroupListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await groupRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var dtos = groups.Select(g => new GroupListItemDto(
            Id: g.Id,
            Name: g.Name,
            Description: g.Description,
            MemberCount: g.UserGroups.Count,
            ServerCount: g.ServerGroups.Count,
            CreatedAt: g.CreatedAt)).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(200, Type = typeof(GroupDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GroupDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return NotFound();
        }

        return Ok(ToDetailDto(group));
    }

    [HttpPost]
    [ProducesResponseType(201, Type = typeof(GroupDetailDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GroupDetailDto>> Create([FromBody] CreateGroupRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Group name is required." });
        }

        var existing = await groupRepository.GetByNameAsync(request.Name.Trim(), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return BadRequest(new { error = $"Group '{request.Name}' already exists." });
        }

        var newGroup = new GroupEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim()
        };

        var created = await groupRepository.CreateAsync(newGroup, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Admin created group '{Name}' (ID: {Id}).", created.Name, created.Id);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDetailDto(created));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(200, Type = typeof(GroupDetailDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GroupDetailDto>> Update(int id, [FromBody] UpdateGroupRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Group name is required." });
        }

        var group = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return NotFound();
        }

        var updated = await groupRepository.UpdateAsync(id, request.Name.Trim(), request.Description?.Trim(), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Admin updated group '{Name}' (ID: {Id}).", updated!.Name, id);

        return Ok(ToDetailDto(updated));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await groupRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return NotFound();
        }

        logger.LogInformation("Admin deleted group ID: {Id}.", id);
        return NoContent();
    }

    [HttpPut("{id:int}/members")]
    [ProducesResponseType(200, Type = typeof(GroupDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GroupDetailDto>> SetMembers(int id, [FromBody] SetGroupMembersRequestDto request, CancellationToken cancellationToken)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return NotFound();
        }

        await groupRepository.SetMembersAsync(id, request.UserIds ?? Array.Empty<int>(), cancellationToken).ConfigureAwait(false);
        var updated = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return Ok(ToDetailDto(updated!));
    }

    [HttpPut("{id:int}/servers")]
    [ProducesResponseType(200, Type = typeof(GroupDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GroupDetailDto>> SetServers(int id, [FromBody] SetGroupServersRequestDto request, CancellationToken cancellationToken)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return NotFound();
        }

        await groupRepository.SetServerAccessAsync(id, request.Servers ?? Array.Empty<ServerGroupAssignmentDto>(), cancellationToken).ConfigureAwait(false);
        var updated = await groupRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return Ok(ToDetailDto(updated!));
    }

    private static GroupDetailDto ToDetailDto(GroupEntity group)
    {
        return new GroupDetailDto(
            Id: group.Id,
            Name: group.Name,
            Description: group.Description,
            Members: group.UserGroups.Select(ug => new GroupMemberDto(
                UserId: ug.User.Id,
                Username: ug.User.Username,
                Email: ug.User.Email,
                Role: ug.User.Role)).ToList(),
            Servers: group.ServerGroups.Select(sg => new GroupServerAccessDto(
                GameServerId: sg.GameServer.Id,
                ServerId: sg.GameServer.ServerId,
                ServerName: sg.GameServer.Name,
                AccessLevel: sg.AccessLevel)).ToList(),
            CreatedAt: group.CreatedAt,
            UpdatedAt: group.UpdatedAt);
    }
}
