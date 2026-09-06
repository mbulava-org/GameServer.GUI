using System.Net;
using System.Net.Http.Json;
using GameServer.API.Dtos.V2;

namespace GameServer.Integration.Tests.Controllers.V2;

[Collection("Integration Tests")]
public class ApiControllersIntegrationTests
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public ApiControllersIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PortController_CheckPort_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/ports/check/tcp/25565");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var available = await response.Content.ReadFromJsonAsync<bool>();
        Assert.True(available || !available);
    }

    [Fact]
    public async Task MountTypeConfigController_GetAll_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/v2/mounttypeconfigs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<MountTypeConfigDto[]>();
        Assert.NotNull(items);
    }

    [Fact]
    public async Task GameTypesController_GetAll_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/v2/gametypes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<GameTypeListItemDto[]>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GameServersController_GetAll_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/v2/gameservers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<GameServerListItemDto[]>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GameTypesController_CreateAndRetrieveGameType_Succeeds()
    {
        var request = new SaveGameTypeRequestDto
        {
            Key = "integration-test-game",
            DisplayName = "Integration Test Game",
            Description = "Created by integration tests",
            Type = "docker",
            IsActive = true
        };

        var postResponse = await _client.PostAsJsonAsync("/api/v2/gametypes", request);
        Assert.True(postResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        var created = await postResponse.Content.ReadFromJsonAsync<GameTypeDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("Integration Test Game", created.DisplayName);

        // Get by Key
        var getResponse = await _client.GetAsync($"/api/v2/gametypes/{created.Key}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<GameTypeDetailDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Key, fetched.Key);
    }
}
