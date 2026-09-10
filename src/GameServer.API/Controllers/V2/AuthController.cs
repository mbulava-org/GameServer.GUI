using System.Security.Claims;
using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Route("api/v2/auth")]
public sealed class AuthController(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(200, Type = typeof(LoginResponseDto))]
    [ProducesResponseType(401)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { error = "Username and password are required." });
        }

        var user = await userRepository.GetByUsernameAsync(request.Username.Trim(), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            logger.LogWarning("Invalid password attempt for username: {Username}", request.Username);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login attempt for unactivated user: {Username}", request.Username);
            return Unauthorized(new { error = "Your account is pending activation by an administrator." });
        }

        var groupNames = user.UserGroups.Select(ug => ug.Group.Name).ToList();
        var (token, expiresAt) = tokenService.GenerateToken(user, groupNames);

        await userRepository.UpdateLastLoginAsync(user.Id, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("User '{Username}' logged in successfully.", user.Username);

        return Ok(new LoginResponseDto(
            Token: token,
            UserId: user.Id,
            Username: user.Username,
            Email: user.Email,
            Role: user.Role,
            Groups: groupNames,
            ExpiresAtUtc: expiresAt));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
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

        var newUser = new GameServer.API.Data.V2.UserEntity
        {
            Username = request.Username.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            Role = "User",
            IsActive = false // Newly registered users are inactive until an administrator activates them and assigns groups
        };

        var created = await userRepository.CreateAsync(newUser, null, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("New user registered: '{Username}' (ID: {Id}, IsActive: false).", created.Username, created.Id);

        return StatusCode(201, new { message = "Account created successfully. Your account is pending activation by an administrator before you can log in." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(200, Type = typeof(UserProfileDto))]
    [ProducesResponseType(401)]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var groupNames = user.UserGroups.Select(ug => ug.Group.Name).ToList();
        return Ok(new UserProfileDto(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            Role: user.Role,
            Groups: groupNames,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt));
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { error = "New password must be at least 6 characters long." });
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new { error = "Current password is incorrect." });
        }

        var newHash = passwordHasher.HashPassword(request.NewPassword);
        await userRepository.UpdateAsync(userId, u => u.PasswordHash = newHash, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Password changed successfully for user '{Username}'.", user.Username);
        return Ok(new { message = "Password changed successfully." });
    }
}
