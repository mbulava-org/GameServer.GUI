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
        Assert.Contains(result, c => c.Key == "nfs");
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnStoredValues()
    {
        var repository = new MountTypeConfigRepository(_dbContext);

        var result = await repository.GetByKeyAsync("volume");

        Assert.NotNull(result);
        Assert.Equal("volume", result.Key);
        Assert.Equal("local", result.GetOption("Driver"));
    }

    [Fact]
    public async Task SaveAsync_WhenNoConfigExists_ShouldInsertRow()
    {
        var repository = new MountTypeConfigRepository(_dbContext);
        var update = new MountTypeConfig
        {
            Key = "custom-nfs",
            DisplayName = "NFS volume",
            Options = new Dictionary<string, string>
            {
                ["Driver"] = "vieux/sshfs",
                ["DriverOptionsJson"] = "{\"type\":\"nfs\",\"device\":\":/volume1/gameservers\",\"o\":\"addr=10.0.0.5,rw\"}",
                ["SourcePathTemplate"] = "{gameTypeKey}_{serverId}_{Source}"
            }
        };

        await repository.SaveAsync(update);

        var stored = Assert.Single(_dbContext.MountTypeConfigs.Where(e => e.Key == "custom-nfs").ToList());
        Assert.Contains("vieux/sshfs", stored.OptionsJson);
        Assert.Contains("10.0.0.5", stored.OptionsJson);
    }

    [Fact]
    public async Task SaveAsync_WhenConfigExists_ShouldUpdateRow()
    {
        var repository = new MountTypeConfigRepository(_dbContext);
        var update = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Volume updated",
            Options = new Dictionary<string, string>
            {
                ["Driver"] = "rexray/ebs",
                ["SourcePathTemplate"] = "{gameTypeKey}_{serverId}_{Source}"
            }
        };

        var result = await repository.SaveAsync(update);

        Assert.Equal("Volume updated", result.DisplayName);
        Assert.Equal("rexray/ebs", result.GetOption("Driver"));
        var stored = _dbContext.MountTypeConfigs.Single(e => e.Key == "volume");
        Assert.Contains("rexray/ebs", stored.OptionsJson);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveRow()
    {
        _dbContext.MountTypeConfigs.Add(new MountTypeConfigEntity
        {
            Key = "custom",
            DisplayName = "Custom mount",
            OptionsJson = "{\"Driver\":\"local\",\"SourcePathTemplate\":\"{Source}\"}"
        });
        await _dbContext.SaveChangesAsync();
        var repository = new MountTypeConfigRepository(_dbContext);

        await repository.DeleteAsync("custom");

        Assert.DoesNotContain(_dbContext.MountTypeConfigs, e => e.Key == "custom");
    }
}
