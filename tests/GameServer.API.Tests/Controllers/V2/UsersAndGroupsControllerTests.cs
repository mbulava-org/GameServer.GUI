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

public sealed class UsersAndGroupsControllerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IGroupRepository> _groupRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    [Fact]
    public async Task UsersController_GetAll_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<UserEntity>
        {
            new() { Id = 1, Username = "admin", Email = "admin@example.com", Role = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { Id = 2, Username = "manager", Email = "manager@example.com", Role = "GameManager", IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        _userRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var controller = new UsersController(_userRepository.Object, _passwordHasher.Object, NullLogger<UsersController>.Instance);

        // Act
        var actionResult = await controller.GetAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedUsers = Assert.IsAssignableFrom<IReadOnlyList<UserListItemDto>>(okResult.Value);
        Assert.Equal(2, returnedUsers.Count);
    }

    [Fact]
    public async Task UsersController_Create_WithValidDto_ReturnsCreated()
    {
        // Arrange
        var request = new CreateUserRequestDto("newuser", "new@example.com", "ValidPassword123!", "User", null);

        _userRepository.Setup(r => r.GetByUsernameAsync("newuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        _passwordHasher.Setup(p => p.HashPassword("ValidPassword123!"))
            .Returns("100000.salt.key");

        var createdEntity = new UserEntity
        {
            Id = 10,
            Username = "newuser",
            Email = "new@example.com",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userRepository.Setup(r => r.CreateAsync(It.IsAny<UserEntity>(), It.IsAny<IEnumerable<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        var controller = new UsersController(_userRepository.Object, _passwordHasher.Object, NullLogger<UsersController>.Instance);

        // Act
        var actionResult = await controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var createdDto = Assert.IsType<UserDetailDto>(createdResult.Value);
        Assert.Equal("newuser", createdDto.Username);
    }

    [Fact]
    public async Task GroupsController_GetAll_ReturnsAllGroups()
    {
        // Arrange
        var groups = new List<GroupEntity>
        {
            new() { Id = 1, Name = "Administrators", Description = "Admin group", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, Name = "Default", Description = "Default group", CreatedAt = DateTime.UtcNow }
        };

        _groupRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        var controller = new GroupsController(_groupRepository.Object, NullLogger<GroupsController>.Instance);

        // Act
        var actionResult = await controller.GetAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedGroups = Assert.IsAssignableFrom<IReadOnlyList<GroupListItemDto>>(okResult.Value);
        Assert.Equal(2, returnedGroups.Count);
    }
}
