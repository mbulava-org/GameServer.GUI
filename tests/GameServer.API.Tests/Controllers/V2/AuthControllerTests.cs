using GameServer.API.Controllers.V2;
using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Controllers.V2;

public sealed class AuthControllerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();

    private AuthController CreateController() =>
        new(_userRepository.Object, _passwordHasher.Object, _tokenService.Object, NullLogger<AuthController>.Instance);

    [Fact]
    public async Task Register_WhenValid_CreatesInactiveUserWithUserRole()
    {
        // Arrange
        var request = new RegisterRequestDto("testuser", "test@example.com", "Password123!");
        _userRepository.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        _passwordHasher.Setup(p => p.HashPassword("Password123!"))
            .Returns("hashed-pw");

        UserEntity? capturedUser = null;
        _userRepository.Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<int>?>(), It.IsAny<CancellationToken>()))
            .Callback<UserEntity, IEnumerable<int>?, CancellationToken>((u, _, _) => capturedUser = u)
            .ReturnsAsync(new UserEntity { Id = 5, Username = "testuser", Role = "User", IsActive = false });

        var controller = CreateController();

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, statusResult.StatusCode);
        Assert.NotNull(capturedUser);
        Assert.Equal("testuser", capturedUser.Username);
        Assert.Equal("User", capturedUser.Role);
        Assert.False(capturedUser.IsActive);
    }

    [Fact]
    public async Task Register_WhenUsernameTaken_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequestDto("existinguser", null, "Password123!");
        _userRepository.Setup(r => r.GetByUsernameAsync("existinguser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserEntity { Id = 1, Username = "existinguser" });

        var controller = CreateController();

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task Login_WhenUserIsNotActive_ReturnsUnauthorizedWithPendingActivationMessage()
    {
        // Arrange
        var request = new LoginRequestDto("inactiveuser", "Password123!");
        var user = new UserEntity
        {
            Id = 3,
            Username = "inactiveuser",
            PasswordHash = "hashed-pw",
            IsActive = false,
            Role = "User"
        };

        _userRepository.Setup(r => r.GetByUsernameAsync("inactiveuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher.Setup(p => p.VerifyPassword("hashed-pw", "Password123!"))
            .Returns(true);

        var controller = CreateController();

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_WhenUserIsActiveAndValid_ReturnsToken()
    {
        // Arrange
        var request = new LoginRequestDto("activeuser", "Password123!");
        var user = new UserEntity
        {
            Id = 4,
            Username = "activeuser",
            Email = "active@example.com",
            PasswordHash = "hashed-pw",
            IsActive = true,
            Role = "User"
        };

        _userRepository.Setup(r => r.GetByUsernameAsync("activeuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasher.Setup(p => p.VerifyPassword("hashed-pw", "Password123!"))
            .Returns(true);

        _tokenService.Setup(t => t.GenerateToken(user, It.IsAny<IReadOnlyList<string>>()))
            .Returns(("jwt-token-xyz", DateTime.UtcNow.AddHours(8)));

        var controller = CreateController();

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var loginResponse = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.Equal("jwt-token-xyz", loginResponse.Token);
        Assert.Equal("activeuser", loginResponse.Username);
    }
}
