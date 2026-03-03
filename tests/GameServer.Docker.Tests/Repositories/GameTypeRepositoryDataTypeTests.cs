using GameServer.Docker.Data;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Repositories;

/// <summary>
/// Tests for GameTypeRepository DataType validation and normalization.
/// These tests verify the fix for the CHECK constraint violation bug.
/// </summary>
public class GameTypeRepositoryDataTypeTests : IDisposable
{
    private readonly GameServerDbContext _context;
    private readonly Mock<ILogger<GameTypeRepository>> _mockLogger;
    private readonly GameTypeRepository _repository;
    private readonly string _dbPath;

    public GameTypeRepositoryDataTypeTests()
    {
        // Use SQLite in-memory database (not EF Core InMemory)
        _dbPath = $":memory:_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<GameServerDbContext>()
            .UseSqlite($"DataSource={_dbPath};Mode=Memory;Cache=Shared")
            .Options;

        _context = new GameServerDbContext(options);
        _context.Database.OpenConnection(); // Keep in-memory database alive
        _context.Database.EnsureCreated(); // Create schema

        _mockLogger = new Mock<ILogger<GameTypeRepository>>();
        _repository = new GameTypeRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region Valid DataType Tests

    [Theory]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("enum")]
    [InlineData("list")]
    [InlineData("port")]
    [InlineData("timezone")]
    public async Task SaveExtendedMetadata_WithValidDataType_ShouldSucceed(string dataType)
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game");
        var metadata = CreateTestMetadata(gameType.Key, dataType);

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dataType, result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Theory]
    [InlineData("STRING", "string")]
    [InlineData("Number", "number")]
    [InlineData("BOOLEAN", "boolean")]
    [InlineData("EnUm", "enum")]
    [InlineData("LIST", "list")]
    [InlineData("Port", "port")]
    [InlineData("TimeZone", "timezone")]
    public async Task SaveExtendedMetadata_WithMixedCaseDataType_ShouldNormalizeToLowercase(string inputType, string expectedType)
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game-case");
        var metadata = CreateTestMetadata(gameType.Key, inputType);

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedType, result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Null/Empty DataType Tests

    [Fact]
    public async Task SaveExtendedMetadata_WithNullDataType_ShouldSucceed()
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game-null");
        var metadata = CreateTestMetadata(gameType.Key, null);

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task SaveExtendedMetadata_WithEmptyStringDataType_ShouldConvertToNull()
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game-empty");
        var metadata = CreateTestMetadata(gameType.Key, "");

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task SaveExtendedMetadata_WithWhitespaceDataType_ShouldConvertToNull()
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game-whitespace");
        var metadata = CreateTestMetadata(gameType.Key, "   ");

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Invalid DataType Tests

    [Theory]
    [InlineData("invalid")]
    [InlineData("text")]
    [InlineData("integer")]
    [InlineData("float")]
    [InlineData("date")]
    [InlineData("datetime")]
    [InlineData("json")]
    public async Task SaveExtendedMetadata_WithInvalidDataType_ShouldConvertToNull(string invalidType)
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync($"test-game-invalid-{invalidType}");
        var metadata = CreateTestMetadata(gameType.Key, invalidType);

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        // Invalid types should be normalized to null (allowed by constraint)
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Multiple Settings Tests

    [Fact]
    public async Task SaveExtendedMetadata_WithMultipleSettings_ShouldNormalizeAllDataTypes()
    {
        // Arrange
        var gameType = await CreateTestGameTypeAsync("test-game-multiple");
        var metadata = new GameTypeExtendedMetadata
        {
            GameTypeKey = gameType.Key,
            EnableTTY = false,
            SettingsMetadata = new Dictionary<string, SettingMetadata>
            {
                ["STRING_SETTING"] = new() { Key = "STRING_SETTING", DataType = "STRING" },
                ["NUMBER_SETTING"] = new() { Key = "NUMBER_SETTING", DataType = "number" },
                ["INVALID_SETTING"] = new() { Key = "INVALID_SETTING", DataType = "invalid" },
                ["NULL_SETTING"] = new() { Key = "NULL_SETTING", DataType = null },
                ["EMPTY_SETTING"] = new() { Key = "EMPTY_SETTING", DataType = "" },
                ["TIMEZONE_SETTING"] = new() { Key = "TIMEZONE_SETTING", DataType = "timezone" }
            }
        };

        // Add corresponding default settings
        foreach (var key in metadata.SettingsMetadata.Keys)
        {
            gameType.DefaultSettings[key] = "test_value";
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("string", result.SettingsMetadata["STRING_SETTING"].DataType); // Normalized to lowercase
        Assert.Equal("number", result.SettingsMetadata["NUMBER_SETTING"].DataType);
        Assert.Null(result.SettingsMetadata["INVALID_SETTING"].DataType); // Invalid converted to null
        Assert.Null(result.SettingsMetadata["NULL_SETTING"].DataType);
        Assert.Null(result.SettingsMetadata["EMPTY_SETTING"].DataType); // Empty converted to null
        Assert.Equal("timezone", result.SettingsMetadata["TIMEZONE_SETTING"].DataType);
    }

    #endregion

    #region Update Existing Metadata Tests

    [Fact]
    public async Task SaveExtendedMetadata_UpdateExistingWithInvalidDataType_ShouldNormalize()
    {
        // Arrange - Create initial metadata with valid type
        var gameType = await CreateTestGameTypeAsync("test-game-update");
        var initialMetadata = CreateTestMetadata(gameType.Key, "string");
        await _repository.SaveExtendedMetadataAsync(gameType.Key, initialMetadata);

        // Act - Update with invalid type
        var updatedMetadata = CreateTestMetadata(gameType.Key, "invalid_type");
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, updatedMetadata);

        // Assert - Should be normalized to null
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task SaveExtendedMetadata_UpdateExistingWithValidDataType_ShouldUpdate()
    {
        // Arrange - Create initial metadata
        var gameType = await CreateTestGameTypeAsync("test-game-update-valid");
        var initialMetadata = CreateTestMetadata(gameType.Key, "string");
        await _repository.SaveExtendedMetadataAsync(gameType.Key, initialMetadata);

        // Act - Update with different valid type
        var updatedMetadata = CreateTestMetadata(gameType.Key, "number");
        var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, updatedMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("number", result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Helper Methods

    private async Task<GameTypeDefinition> CreateTestGameTypeAsync(string key)
    {
        var gameType = new GameTypeDefinition
        {
            Key = key,
            DisplayName = $"Test {key}",
            Description = "Test game type",
            Image = "test:latest",
            Ports = new List<PortDefinition>(),
            Volumes = new List<VolumeDefinition>(),
            DefaultSettings = new Dictionary<string, string>
            {
                ["TEST_SETTING"] = "test_value"
            }
        };

        await _repository.CreateAsync(gameType);
        return gameType;
    }

    private GameTypeExtendedMetadata CreateTestMetadata(string gameTypeKey, string? dataType)
    {
        return new GameTypeExtendedMetadata
        {
            GameTypeKey = gameTypeKey,
            EnableTTY = false,
            SettingsMetadata = new Dictionary<string, SettingMetadata>
            {
                ["TEST_SETTING"] = new SettingMetadata
                {
                    Key = "TEST_SETTING",
                    DataType = dataType,
                    Description = "Test setting",
                    DisplayOrder = 0
                }
            }
        };
    }

    #endregion
}
