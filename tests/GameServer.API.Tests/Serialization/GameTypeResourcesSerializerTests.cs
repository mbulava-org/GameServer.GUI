using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;
using GameServer.API.Services.V2;

namespace GameServer.API.Tests.Serialization;

public class GameTypeResourcesSerializerTests
{
    [Fact]
    public void Serialize_WhenNull_ShouldReturnNull()
    {
        var json = GameTypeResourcesSerializer.Serialize((GameTypeRevisionResources?)null);
        Assert.Null(json);
    }

    [Fact]
    public void ParseModel_WhenNullOrWhitespace_ShouldReturnNull()
    {
        Assert.Null(GameTypeResourcesSerializer.ParseModel(null));
        Assert.Null(GameTypeResourcesSerializer.ParseModel(string.Empty));
        Assert.Null(GameTypeResourcesSerializer.ParseModel("   "));
    }

    [Fact]
    public void ParseDto_WhenNullOrWhitespace_ShouldReturnNull()
    {
        Assert.Null(GameTypeResourcesSerializer.ParseDto(null));
        Assert.Null(GameTypeResourcesSerializer.ParseDto(string.Empty));
        Assert.Null(GameTypeResourcesSerializer.ParseDto("   "));
    }

    [Fact]
    public void RoundTrip_Model_ShouldPreserveAllProperties()
    {
        var original = new GameTypeRevisionResources
        {
            CpuLimitCores = 4.0m,
            CpuReservationCores = 2.0m,
            MemoryLimitBytes = 8L * 1024 * 1024 * 1024,
            MemoryReservationBytes = 4L * 1024 * 1024 * 1024,
            MemoryLimitVariable = "MEM_LIMIT",
            MemoryReservationVariable = "MEM_RES",
            PidsLimit = 1000,
            MaxReplicasPerNode = 1,
            Constraints =
            [
                new PlacementConstraint
                {
                    Target = "node.role",
                    Operator = "==",
                    Value = "worker"
                },
                new PlacementConstraint
                {
                    Target = "node.labels.zone",
                    Operator = "==",
                    Value = "us-east-1a"
                }
            ],
            Preferences =
            [
                new PlacementPreference
                {
                    Strategy = "spread",
                    Descriptor = "node.labels.zone"
                }
            ]
        };

        var json = GameTypeResourcesSerializer.Serialize(original);
        Assert.NotNull(json);

        var deserialized = GameTypeResourcesSerializer.ParseModel(json);
        Assert.NotNull(deserialized);

        Assert.Equal(original.CpuLimitCores, deserialized.CpuLimitCores);
        Assert.Equal(original.CpuReservationCores, deserialized.CpuReservationCores);
        Assert.Equal(original.MemoryLimitBytes, deserialized.MemoryLimitBytes);
        Assert.Equal(original.MemoryReservationBytes, deserialized.MemoryReservationBytes);
        Assert.Equal(original.MemoryLimitVariable, deserialized.MemoryLimitVariable);
        Assert.Equal(original.MemoryReservationVariable, deserialized.MemoryReservationVariable);
        Assert.Equal(original.PidsLimit, deserialized.PidsLimit);
        Assert.Equal(original.MaxReplicasPerNode, deserialized.MaxReplicasPerNode);

        Assert.Equal(2, deserialized.Constraints.Count);
        Assert.Equal("node.role", deserialized.Constraints[0].Target);
        Assert.Equal("==", deserialized.Constraints[0].Operator);
        Assert.Equal("worker", deserialized.Constraints[0].Value);

        Assert.Single(deserialized.Preferences);
        Assert.Equal("spread", deserialized.Preferences[0].Strategy);
        Assert.Equal("node.labels.zone", deserialized.Preferences[0].Descriptor);
    }

    [Fact]
    public void MapToDto_And_MapToModel_ShouldConvertFaithfully()
    {
        var dto = new GameTypeRevisionResourcesDto
        {
            CpuLimitCores = 2.0m,
            CpuReservationCores = 1.0m,
            MemoryLimitBytes = 4096L * 1024 * 1024,
            MemoryReservationBytes = 2048L * 1024 * 1024,
            PidsLimit = 500,
            MaxReplicasPerNode = 2,
            Constraints = [new PlacementConstraintDto { Target = "node.hostname", Operator = "!=", Value = "backup-node" }],
            Preferences = [new PlacementPreferenceDto { Strategy = "spread", Descriptor = "engine.labels.os" }]
        };

        var model = GameTypeResourcesSerializer.MapToModel(dto);
        var backToDto = GameTypeResourcesSerializer.MapToDto(model);

        Assert.Equal(dto.CpuLimitCores, backToDto.CpuLimitCores);
        Assert.Equal(dto.CpuReservationCores, backToDto.CpuReservationCores);
        Assert.Equal(dto.MemoryLimitBytes, backToDto.MemoryLimitBytes);
        Assert.Equal(dto.MemoryReservationBytes, backToDto.MemoryReservationBytes);
        Assert.Equal(dto.PidsLimit, backToDto.PidsLimit);
        Assert.Equal(dto.MaxReplicasPerNode, backToDto.MaxReplicasPerNode);
        Assert.Single(backToDto.Constraints);
        Assert.Equal("backup-node", backToDto.Constraints[0].Value);
        Assert.Single(backToDto.Preferences);
        Assert.Equal("engine.labels.os", backToDto.Preferences[0].Descriptor);
    }
}
