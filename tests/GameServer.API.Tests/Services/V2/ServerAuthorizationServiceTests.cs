using System.Security.Claims;
using GameServer.API.Data.V2;
using GameServer.API.Services.V2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public sealed class ServerAuthorizationServiceTests : IDisposable
{
    private readonly SqliteGameServerV2DbContext _context;
    private readonly ServerAuthorizationService _service;

    public ServerAuthorizationServiceTests()
    {
        var dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<SqliteGameServerV2DbContext>()
            .UseSqlite($"DataSource={dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new SqliteGameServerV2DbContext(options);
        _context.Database.OpenConnection();
        _context.Database.Migrate();

        _service = new ServerAuthorizationService(_context, Mock.Of<ILogger<ServerAuthorizationService>>());
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private async Task SeedDataAsync()
    {
        var gameType = new GameTypeEntity
        {
            Key = "test-game",
            DisplayName = "Test Game",
            Type = "docker",
            Revisions =
            [
                new GameTypeRevisionEntity
                {
                    ImageReference = "test/img",
                    VersionTag = "1.0",
                    IsPublished = true
                }
            ]
        };
        _context.GameTypes.Add(gameType);
        await _context.SaveChangesAsync();

        var revision = gameType.Revisions.First();

        var server1 = new GameServerEntity
        {
            ServerId = "srv-1",
            Name = "Server 1",
            ServiceName = "service-1",
            Status = "Running",
            GameTypeRevisionId = revision.Id
        };
        var server2 = new GameServerEntity
        {
            ServerId = "srv-2",
            Name = "Server 2",
            ServiceName = "service-2",
            Status = "Running",
            GameTypeRevisionId = revision.Id
        };
        _context.GameServers.AddRange(server1, server2);
        await _context.SaveChangesAsync();

        var groupView = new GroupEntity { Name = "Viewers Group" };
        var groupEdit = new GroupEntity { Name = "Editors Group" };
        _context.Groups.AddRange(groupView, groupEdit);
        await _context.SaveChangesAsync();

        var userViewOnly = new UserEntity
        {
            Username = "viewer",
            PasswordHash = "hash",
            Role = "User"
        };
        var userEditor = new UserEntity
        {
            Username = "editor",
            PasswordHash = "hash",
            Role = "User"
        };
        var userUnassigned = new UserEntity
        {
            Username = "unassigned",
            PasswordHash = "hash",
            Role = "User"
        };
        _context.Users.AddRange(userViewOnly, userEditor, userUnassigned);
        await _context.SaveChangesAsync();

        _context.UserGroups.AddRange(
            new UserGroupEntity { UserId = userViewOnly.Id, GroupId = groupView.Id },
            new UserGroupEntity { UserId = userEditor.Id, GroupId = groupEdit.Id }
        );

        _context.GameServerGroups.AddRange(
            new GameServerGroupEntity { GameServerId = server1.Id, GroupId = groupView.Id, AccessLevel = "View" },
            new GameServerGroupEntity { GameServerId = server2.Id, GroupId = groupEdit.Id, AccessLevel = "Edit" }
        );

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task AdminUser_CanViewAndEditAnyServer()
    {
        // Arrange
        await SeedDataAsync();

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin")
        ], "Test"));

        // Act
        var canView = await _service.CanViewServerAsync(adminPrincipal, "srv-1");
        var canEdit = await _service.CanEditServerAsync(adminPrincipal, "srv-1");
        var accessibleIds = await _service.GetAccessibleServerIdsAsync(adminPrincipal);
        var isAdmin = _service.IsAdmin(adminPrincipal);

        // Assert
        Assert.True(isAdmin);
        Assert.True(canView);
        Assert.True(canEdit);
        Assert.Null(accessibleIds); // null indicates unconstrained access
    }

    [Fact]
    public async Task UserWithoutGroups_CannotViewOrEditServer()
    {
        // Arrange
        await SeedDataAsync();
        var unassignedUser = await _context.Users.FirstAsync(u => u.Username == "unassigned");

        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, unassignedUser.Id.ToString()),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act
        var canView = await _service.CanViewServerAsync(userPrincipal, "srv-1");
        var canEdit = await _service.CanEditServerAsync(userPrincipal, "srv-1");
        var accessibleIds = await _service.GetAccessibleServerIdsAsync(userPrincipal);
        var isAdmin = _service.IsAdmin(userPrincipal);

        // Assert
        Assert.False(isAdmin);
        Assert.False(canView);
        Assert.False(canEdit);
        Assert.NotNull(accessibleIds);
        Assert.Empty(accessibleIds);
    }

    [Fact]
    public async Task UserWithViewGroup_CanViewButNotEditServer()
    {
        // Arrange
        await SeedDataAsync();
        var viewerUser = await _context.Users.FirstAsync(u => u.Username == "viewer");

        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, viewerUser.Id.ToString()),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act
        var canView = await _service.CanViewServerAsync(userPrincipal, "srv-1");
        var canEdit = await _service.CanEditServerAsync(userPrincipal, "srv-1");
        var accessibleIds = await _service.GetAccessibleServerIdsAsync(userPrincipal);

        // Assert
        Assert.True(canView);
        Assert.False(canEdit);
        Assert.NotNull(accessibleIds);
        Assert.Contains("srv-1", accessibleIds);
        Assert.DoesNotContain("srv-2", accessibleIds);
    }

    [Fact]
    public async Task UserWithEditGroup_CanViewAndEditServer()
    {
        // Arrange
        await SeedDataAsync();
        var editorUser = await _context.Users.FirstAsync(u => u.Username == "editor");

        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, editorUser.Id.ToString()),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act
        var canView = await _service.CanViewServerAsync(userPrincipal, "srv-2");
        var canEdit = await _service.CanEditServerAsync(userPrincipal, "srv-2");
        var accessibleIds = await _service.GetAccessibleServerIdsAsync(userPrincipal);

        // Assert
        Assert.True(canView);
        Assert.True(canEdit);
        Assert.NotNull(accessibleIds);
        Assert.Contains("srv-2", accessibleIds);
        Assert.DoesNotContain("srv-1", accessibleIds);
    }

    [Fact]
    public async Task Creator_CanViewAndEditServer_EvenWithoutGroupMembership()
    {
        // Arrange
        await SeedDataAsync();
        var unassignedUser = await _context.Users.FirstAsync(u => u.Username == "unassigned");

        var server3 = new GameServerEntity
        {
            ServerId = "srv-3",
            Name = "Server 3 (Creator Owned)",
            ServiceName = "service-3",
            Status = "Running",
            GameTypeRevisionId = (await _context.GameTypeRevisions.FirstAsync()).Id,
            CreatedByUserId = unassignedUser.Id
        };
        _context.GameServers.Add(server3);
        await _context.SaveChangesAsync();

        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, unassignedUser.Id.ToString()),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act
        var canView = await _service.CanViewServerAsync(userPrincipal, "srv-3");
        var canEdit = await _service.CanEditServerAsync(userPrincipal, "srv-3");
        var accessibleIds = await _service.GetAccessibleServerIdsAsync(userPrincipal);

        // Assert
        Assert.True(canView);
        Assert.True(canEdit);
        Assert.NotNull(accessibleIds);
        Assert.Contains("srv-3", accessibleIds);
    }

    [Fact]
    public void CanViewPassword_AdminAndCreator_AlwaysReturnsTrue()
    {
        // Arrange
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "99"),
            new Claim(ClaimTypes.Role, "Admin")
        ], "Test"));

        var creatorPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act & Assert
        Assert.True(_service.CanViewPassword(adminPrincipal, serverCreatedByUserId: 10, accessPolicy: "Individual", allowedUserIds: [1, 2]));
        Assert.True(_service.CanViewPassword(creatorPrincipal, serverCreatedByUserId: 42, accessPolicy: "Individual", allowedUserIds: [1, 2]));
    }

    [Fact]
    public void CanViewPassword_GroupPolicy_ReturnsTrueForStandardUser()
    {
        // Arrange
        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act & Assert
        Assert.True(_service.CanViewPassword(userPrincipal, serverCreatedByUserId: 10, accessPolicy: "Group", allowedUserIds: []));
        Assert.True(_service.CanViewPassword(userPrincipal, serverCreatedByUserId: 10, accessPolicy: null, allowedUserIds: null));
    }

    [Fact]
    public void CanViewPassword_IndividualPolicy_ReturnsTrueOnlyForAllowedUser()
    {
        // Arrange
        var allowedUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "5"),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        var disallowedUser = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "6"),
            new Claim(ClaimTypes.Role, "User")
        ], "Test"));

        // Act & Assert
        Assert.True(_service.CanViewPassword(allowedUser, serverCreatedByUserId: 10, accessPolicy: "Individual", allowedUserIds: [5, 8]));
        Assert.False(_service.CanViewPassword(disallowedUser, serverCreatedByUserId: 10, accessPolicy: "Individual", allowedUserIds: [5, 8]));
    }
}
