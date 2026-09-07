using Bunit;
using GameServer.Web.Components.Pages.Admin;
using GameServer.Web.Models;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.Auth;
using GameServer.Web.Services.V2;
using GameServer.Web.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.Admin;

public sealed class AdminScreensTests : BunitContext
{
    private readonly Mock<IAuthApiService> _mockAuthApi = new();
    private readonly Mock<IGameServerV2ApiService> _mockServerApi = new();

    public AdminScreensTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddTestAuthServices("admin", "Admin");
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<ContextMenuService>();
        Services.AddSingleton(_mockAuthApi.Object);
        Services.AddSingleton(_mockServerApi.Object);
    }

    [Fact]
    public void UsersManager_WhenLoaded_ShouldRenderUserGridWithRolesAndGroups()
    {
        // Arrange
        var groups = new List<GroupModel>
        {
            new(1, "Gamers", "General gamers", 2, 1, DateTime.UtcNow),
            new(2, "Moderators", "Server mods", 1, 1, DateTime.UtcNow)
        };

        var users = new List<UserModel>
        {
            new(1, "admin", "admin@test.local", "Admin", true, ["Gamers", "Moderators"], DateTime.UtcNow, DateTime.UtcNow),
            new(2, "manager", "manager@test.local", "GameManager", true, ["Gamers"], DateTime.UtcNow, DateTime.UtcNow),
            new(3, "player", "player@test.local", "User", false, [], DateTime.UtcNow, null)
        };

        _mockAuthApi.Setup(a => a.GetGroupsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        _mockAuthApi.Setup(a => a.GetUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        // Act
        var cut = Render<UsersManager>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("User Management", cut.Markup);
            Assert.Contains("admin", cut.Markup);
            Assert.Contains("manager", cut.Markup);
            Assert.Contains("player", cut.Markup);
            Assert.Contains("Admin", cut.Markup);
            Assert.Contains("GameManager", cut.Markup);
            Assert.Contains("Gamers", cut.Markup);
            Assert.Contains("Moderators", cut.Markup);
            Assert.Contains("Active", cut.Markup);
            Assert.Contains("Disabled", cut.Markup);
        });
    }

    [Fact]
    public void GroupsManager_WhenLoaded_ShouldRenderGroupGridWithMemberAndServerCounts()
    {
        // Arrange
        var groups = new List<GroupModel>
        {
            new(1, "Alpha Team", "Alpha testing group", 3, 2, DateTime.UtcNow),
            new(2, "Beta Team", "Beta testing group", 1, 0, DateTime.UtcNow)
        };

        _mockAuthApi.Setup(a => a.GetGroupsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(groups);

        // Act
        var cut = Render<GroupsManager>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Group Management", cut.Markup);
            Assert.Contains("Alpha Team", cut.Markup);
            Assert.Contains("Beta Team", cut.Markup);
            Assert.Contains("3 users", cut.Markup);
            Assert.Contains("2 servers", cut.Markup);
            Assert.Contains("1 users", cut.Markup);
            Assert.Contains("0 servers", cut.Markup);
        });
    }

    [Fact]
    public void CreateUserDialog_ShouldRenderFields()
    {
        // Arrange
        var availableGroups = new List<GroupModel>
        {
            new(1, "Gamers", "General gamers", 2, 1, DateTime.UtcNow)
        };

        // Act
        var cut = Render<CreateUserDialog>(parameters => parameters
            .Add(p => p.Model, new UsersManager.CreateUserViewModel())
            .Add(p => p.AvailableGroups, availableGroups));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Username", cut.Markup);
            Assert.Contains("Email", cut.Markup);
            Assert.Contains("Password", cut.Markup);
            Assert.Contains("Role", cut.Markup);
            Assert.Contains("Assigned Groups", cut.Markup);
            Assert.Contains("Create User", cut.Markup);
        });
    }

    [Fact]
    public void EditUserDialog_ShouldRenderFieldsWithExistingValues()
    {
        // Arrange
        var availableGroups = new List<GroupModel>
        {
            new(1, "Gamers", "General gamers", 2, 1, DateTime.UtcNow)
        };

        var editModel = new UsersManager.EditUserViewModel
        {
            Id = 42,
            Username = "johndoe",
            Email = "john@example.com",
            Role = "GameManager",
            IsActive = true,
            SelectedGroupIds = [1]
        };

        // Act
        var cut = Render<EditUserDialog>(parameters => parameters
            .Add(p => p.Model, editModel)
            .Add(p => p.AvailableGroups, availableGroups));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("User: johndoe", cut.Markup);
            Assert.Contains("Email", cut.Markup);
            Assert.Contains("Role", cut.Markup);
            Assert.Contains("New Password", cut.Markup);
            Assert.Contains("Account is active", cut.Markup);
            Assert.Contains("Save Changes", cut.Markup);
        });
    }

    [Fact]
    public void GroupMembersDialog_ShouldRenderSelectedMembers()
    {
        // Arrange
        var allUsers = new List<UserModel>
        {
            new(1, "alice", "alice@example.com", "Admin", true, ["Gamers"], DateTime.UtcNow, DateTime.UtcNow),
            new(2, "bob", "bob@example.com", "User", true, ["Gamers"], DateTime.UtcNow, DateTime.UtcNow)
        };

        var groupDetail = new GroupDetailModel(
            1,
            "Gamers",
            "Gamers group",
            [
                new GroupMemberModel(1, "alice", "alice@example.com", "Admin"),
                new GroupMemberModel(2, "bob", "bob@example.com", "User")
            ],
            [],
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Act
        var cut = Render<GroupMembersDialog>(parameters => parameters
            .Add(p => p.Group, groupDetail)
            .Add(p => p.AllUsers, allUsers));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Gamers", cut.Markup);
            Assert.Contains("alice (Admin)", cut.Markup);
            Assert.Contains("bob (User)", cut.Markup);
            Assert.Contains("Save Members", cut.Markup);
        });
    }

    [Fact]
    public void GroupServersDialog_ShouldRenderServerRowsAndAccessOptions()
    {
        // Arrange
        var servers = new List<GameServerListItem>
        {
            new()
            {
                Id = 1,
                ServerId = "srv-1",
                Name = "Survival Server",
                GameTypeDisplayName = "Minecraft",
                Status = "Running"
            }
        };

        var groupDetail = new GroupDetailModel(
            1,
            "Gamers",
            "Gamers group",
            [],
            [
                new GroupServerAccessModel(1, "srv-1", "Survival Server", "Edit")
            ],
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Act
        var cut = Render<GroupServersDialog>(parameters => parameters
            .Add(p => p.Group, groupDetail)
            .Add(p => p.AllServers, servers));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Survival Server", cut.Markup);
            Assert.Contains("srv-1", cut.Markup);
            Assert.Contains("Minecraft", cut.Markup);
            Assert.Contains("Save Server Access", cut.Markup);
        });
    }
}
