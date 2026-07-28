using GameServer.Docker.Data.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Docker.Tests.Repositories.V2;

public class MountTypeConfigRepositoryTests : IDisposable
{
    private readonly GameServerV2DbContext _dbContext;

    public MountTypeConfigRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<GameServerV2DbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _dbContext = new GameServerV2DbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnSeededDefaults()
    {
        var repository = new MountTypeConfigRepository(_dbContext);

        var result = await repository.GetAllAsync();

        Assert.NotEmpty(result);
        Assert.Contains(result, c => c.Key == "volume");
        Assert.Contains(result, c => c.Key == "bind");
        Assert.Contains(result, c => c.Key == "tmpfs");
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnStoredValues()
    {
        var repository = new MountTypeConfigRepository(_dbContext);

        var result = await repository.GetByKeyAsync("volume");

        Assert.NotNull(result);
        Assert.Equal("volume", result.Key);
        Assert.Equal("local", result.Driver);
    }

    [Fact]
    public async Task SaveAsync_WhenNoConfigExists_ShouldInsertRow()
    {
        var repository = new MountTypeConfigRepository(_dbContext);
        var update = new MountTypeConfig
        {
            Key = "nfs",
            DisplayName = "NFS volume",
            Driver = "vieux/sshfs",
            DriverOptionsJson = "{\"type\":\"nfs\",\"device\":\":/volume1/gameservers\",\"o\":\"addr=10.0.0.5,rw\"}",
            SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
            ContainerPathTemplate = "{Source}"
        };

        await repository.SaveAsync(update);

        var stored = Assert.Single(_dbContext.MountTypeConfigs.Where(e => e.Key == "nfs").ToList());
        Assert.Equal("vieux/sshfs", stored.Driver);
        Assert.Contains("10.0.0.5", stored.DriverOptionsJson);
    }

    [Fact]
    public async Task SaveAsync_WhenConfigExists_ShouldUpdateRow()
    {
        var repository = new MountTypeConfigRepository(_dbContext);
        var update = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Volume updated",
            Driver = "rexray/ebs",
            SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
            ContainerPathTemplate = "{Source}"
        };

        var result = await repository.SaveAsync(update);

        Assert.Equal("Volume updated", result.DisplayName);
        var stored = _dbContext.MountTypeConfigs.Single(e => e.Key == "volume");
        Assert.Equal("rexray/ebs", stored.Driver);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRow()
    {
        _dbContext.MountTypeConfigs.Add(new MountTypeConfigEntity
        {
            Key = "custom",
            DisplayName = "Custom mount",
            Driver = "local",
            SourcePathTemplate = "{Source}",
            ContainerPathTemplate = "{Source}"
        });
        await _dbContext.SaveChangesAsync();
        var repository = new MountTypeConfigRepository(_dbContext);

        await repository.DeleteAsync("custom");

        Assert.DoesNotContain(_dbContext.MountTypeConfigs, e => e.Key == "custom");
    }
}
