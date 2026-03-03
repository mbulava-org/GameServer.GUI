using System.Net;
using System.Net.Http.Json;
using GameServer.Docker.Models;
using GameServer.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameServer.Integration.Tests.Controllers;

/// <summary>
/// Integration tests for Extended Metadata API endpoints.
/// Tests the complete flow from HTTP request to database with DataType validation.
/// </summary>
public class GameTypeExtendedMetadataControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GameTypeExtendedMetadataControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task PostExtendedMetadata_WithValidDataType_ShouldReturn200(string dataType)
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync($"test-valid-{dataType}");
        var metadata = CreateTestMetadata(gameTypeKey, dataType);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Equal(dataType, result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Theory]
    [InlineData("STRING", "string")]
    [InlineData("Number", "number")]
    [InlineData("TIMEZONE", "timezone")]
    public async Task PostExtendedMetadata_WithMixedCaseDataType_ShouldNormalizeAndReturn200(
        string inputType, string expectedType)
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync($"test-case-{inputType}");
        var metadata = CreateTestMetadata(gameTypeKey, inputType);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Equal(expectedType, result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Null/Empty DataType Tests

    [Fact]
    public async Task PostExtendedMetadata_WithNullDataType_ShouldReturn200()
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync("test-null-datatype");
        var metadata = CreateTestMetadata(gameTypeKey, null);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task PostExtendedMetadata_WithEmptyStringDataType_ShouldNormalizeToNullAndReturn200()
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync("test-empty-datatype");
        var metadata = CreateTestMetadata(gameTypeKey, "");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Invalid DataType Tests

    [Theory]
    [InlineData("invalid")]
    [InlineData("text")]
    [InlineData("int")]
    [InlineData("float")]
    public async Task PostExtendedMetadata_WithInvalidDataType_ShouldNormalizeToNullAndReturn200(string invalidType)
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync($"test-invalid-{invalidType}");
        var metadata = CreateTestMetadata(gameTypeKey, invalidType);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        // Should succeed (200) but normalize invalid type to null
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        // Invalid type should be normalized to null (graceful handling)
        Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Regression Tests (Previously Failing Scenarios)

    [Fact]
    public async Task PostExtendedMetadata_WithTimezoneDataType_ShouldSucceed()
    {
        // This was the original bug - timezone type was missing from UI dropdown
        // but existed in database constraint, causing CHECK constraint violation

        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync("test-regression-timezone");
        var metadata = CreateTestMetadata(gameTypeKey, "timezone");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Equal("timezone", result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task PostExtendedMetadata_MultipleTimesWithDifferentDataTypes_ShouldSucceed()
    {
        // Test updating metadata multiple times (common user workflow)

        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync("test-multiple-updates");
        
        // Act & Assert - First save with string
        var metadata1 = CreateTestMetadata(gameTypeKey, "string");
        var response1 = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Act & Assert - Update to number
        var metadata2 = CreateTestMetadata(gameTypeKey, "number");
        var response2 = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Act & Assert - Update to timezone
        var metadata3 = CreateTestMetadata(gameTypeKey, "timezone");
        var response3 = await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata3);
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);

        // Verify final state
        var finalResult = await response3.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(finalResult);
        Assert.Equal("timezone", finalResult.SettingsMetadata["TEST_SETTING"].DataType);
    }

    #endregion

    #region Retrieval Tests

    [Fact]
    public async Task GetExtendedMetadata_AfterSaving_ShouldReturnSavedData()
    {
        // Arrange
        var gameTypeKey = await CreateTestGameTypeAsync("test-get-metadata");
        var metadata = CreateTestMetadata(gameTypeKey, "timezone");
        await _client.PostAsJsonAsync($"/api/gametypes/extended/{gameTypeKey}", metadata);

        // Act
        var response = await _client.GetAsync($"/api/gametypes/extended/{gameTypeKey}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<GameTypeExtendedMetadata>();
        Assert.NotNull(result);
        Assert.Equal("timezone", result.SettingsMetadata["TEST_SETTING"].DataType);
    }

    [Fact]
    public async Task GetExtendedMetadata_WhenNotExists_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/api/gametypes/extended/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestGameTypeAsync(string key)
    {
        var gameType = new GameTypeDefinition
        {
            Key = key,
            DisplayName = $"Test {key}",
            Description = "Integration test game type",
            Image = "test:latest",
            Ports = new List<PortDefinition>(),
            Volumes = new List<VolumeDefinition>(),
            DefaultSettings = new Dictionary<string, string>
            {
                ["TEST_SETTING"] = "test_value"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/gametypes", gameType);
        response.EnsureSuccessStatusCode();

        return key;
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
                    Description = "Integration test setting",
                    DisplayOrder = 0
                }
            },
            CustomProperties = new Dictionary<string, string>()
        };
    }

    #endregion
}
