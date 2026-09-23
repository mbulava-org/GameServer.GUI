using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;
using GameServer.API.Services.V2;

namespace GameServer.API.Tests.Services.V2;

public sealed class GameTypeHealthcheckSerializerTests
{
    [Fact]
    public void ParseDto_WhenJsonNullOrEmpty_ReturnsNull()
    {
        Assert.Null(GameTypeHealthcheckSerializer.ParseDto(null));
        Assert.Null(GameTypeHealthcheckSerializer.ParseDto(string.Empty));
        Assert.Null(GameTypeHealthcheckSerializer.ParseDto("   "));
    }

    [Fact]
    public void ParseDto_WhenJsonInvalid_ReturnsNull()
    {
        Assert.Null(GameTypeHealthcheckSerializer.ParseDto("not json"));
        Assert.Null(GameTypeHealthcheckSerializer.ParseDto("{ invalid }"));
    }

    [Fact]
    public void ParseDto_WhenValidJson_ParsesAllFieldsCorrectly()
    {
        var json = """
        {
            "disable": false,
            "testType": "CMD-SHELL",
            "testCommand": "curl -f http://localhost:8080/health || exit 1",
            "startPeriodSeconds": 300,
            "intervalSeconds": 30,
            "timeoutSeconds": 10,
            "startIntervalSeconds": 5,
            "retries": 3
        }
        """;

        var dto = GameTypeHealthcheckSerializer.ParseDto(json);

        Assert.NotNull(dto);
        Assert.False(dto.Disable);
        Assert.Equal("CMD-SHELL", dto.TestType);
        Assert.Equal("curl -f http://localhost:8080/health || exit 1", dto.TestCommand);
        Assert.Equal(300, dto.StartPeriodSeconds);
        Assert.Equal(30, dto.IntervalSeconds);
        Assert.Equal(10, dto.TimeoutSeconds);
        Assert.Equal(5, dto.StartIntervalSeconds);
        Assert.Equal(3, dto.Retries);
    }

    [Fact]
    public void ParseModel_WhenValidJson_MapsToModelCorrectly()
    {
        var json = """
        {
            "disable": true,
            "startPeriodSeconds": 180
        }
        """;

        var model = GameTypeHealthcheckSerializer.ParseModel(json);

        Assert.NotNull(model);
        Assert.True(model.Disable);
        Assert.Equal(180, model.StartPeriodSeconds);
        Assert.Null(model.TestCommand);
    }

    [Fact]
    public void Serialize_RoundTripModel_PreservesValues()
    {
        var original = new GameTypeRevisionHealthcheck
        {
            Disable = false,
            TestType = "CMD",
            TestCommand = "/app/health.sh",
            StartPeriodSeconds = 600,
            IntervalSeconds = 45,
            TimeoutSeconds = 15,
            StartIntervalSeconds = 10,
            Retries = 5
        };

        var json = GameTypeHealthcheckSerializer.Serialize(original);
        Assert.NotNull(json);

        var restored = GameTypeHealthcheckSerializer.ParseModel(json);
        Assert.NotNull(restored);
        Assert.Equal(original.Disable, restored.Disable);
        Assert.Equal(original.TestType, restored.TestType);
        Assert.Equal(original.TestCommand, restored.TestCommand);
        Assert.Equal(original.StartPeriodSeconds, restored.StartPeriodSeconds);
        Assert.Equal(original.IntervalSeconds, restored.IntervalSeconds);
        Assert.Equal(original.TimeoutSeconds, restored.TimeoutSeconds);
        Assert.Equal(original.StartIntervalSeconds, restored.StartIntervalSeconds);
        Assert.Equal(original.Retries, restored.Retries);
    }

    [Fact]
    public void Serialize_WhenNull_ReturnsNull()
    {
        Assert.Null(GameTypeHealthcheckSerializer.Serialize((GameTypeRevisionHealthcheck?)null));
        Assert.Null(GameTypeHealthcheckSerializer.Serialize((GameTypeRevisionHealthcheckDto?)null));
    }
}
