using System.Security.Claims;
using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Route("api/v2/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<UserListItemDto>))]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var dtos = users.Select(u => new UserListItemDto(
            Id: u.Id,
            Username: u.Username,
            Email: u.Email,
            Role: u.Role,
            IsActive: u.IsActive,
            Groups: u.UserGroups.Select(ug => ug.Group.Name).ToList(),
            CreatedAt: u.CreatedAt,
            LastLoginAt: u.LastLoginAt)).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(200, Type = typeof(UserDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(ToDetailDto(user));
    }

    [HttpPost]
    [ProducesResponseType(201, Type = typeof(UserDetailDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<UserDetailDto>> Create([FromBody] CreateUserRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { error = "Username is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { error = "Password must be at least 6 characters long." });
        }

        var existing = await userRepository.GetByUsernameAsync(request.Username.Trim(), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return BadRequest(new { error = $"Username '{request.Username}' is already taken." });
        }

        var role = NormalizeRole(request.Role);
        var passwordHash = passwordHasher.HashPassword(request.Password);

        var newUser = new UserEntity
        {
            Username = request.Username.Trim(),
            Email = request.Email?.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = request.IsActive ?? true
        };

        var created = await userRepository.CreateAsync(newUser, request.GroupIds, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Admin created user '{Username}' with role '{Role}'.", created.Username, created.Role);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToDetailDto(created));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(200, Type = typeof(UserDetailDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDetailDto>> Update(int id, [FromBody] UpdateUserRequestDto request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return NotFound();
        }

        var updated = await userRepository.UpdateAsync(id, u =>
        {
            if (request.Email is not null)
            {
                u.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                u.Role = NormalizeRole(request.Role);
            }

            if (request.IsActive.HasValue)
            {
                u.IsActive = request.IsActive.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                u.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
            }
        }, request.GroupIds, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Admin updated user '{Username}' (ID: {Id}).", updated!.Username, id);
        return Ok(ToDetailDto(updated));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id)
        {
            return BadRequest(new { error = "You cannot delete your own account." });
        }

        var deleted = await userRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return NotFound();
        }

        logger.LogInformation("Admin deleted user ID: {Id}.", id);
        return NoContent();
    }

    private static string NormalizeRole(string? role)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (string.Equals(role, "GameManager", StringComparison.OrdinalIgnoreCase)) return "GameManager";
        return "User";
    }

    private static UserDetailDto ToDetailDto(UserEntity user)
    {
        return new UserDetailDto(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            Role: user.Role,
            IsActive: user.IsActive,
            Groups: user.UserGroups.Select(ug => new UserGroupMembershipDto(ug.GroupId, ug.Group.Name)).ToList(),
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt,
            LastLoginAt: user.LastLoginAt);
    }
}
