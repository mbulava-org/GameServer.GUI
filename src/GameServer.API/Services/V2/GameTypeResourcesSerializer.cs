using System.Text.Json;
using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Helpers for serializing and deserializing <see cref="GameTypeRevisionResourcesDto"/> and <see cref="GameTypeRevisionResources"/>.
/// </summary>
public static class GameTypeResourcesSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static GameTypeRevisionResourcesDto? ParseDto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GameTypeRevisionResourcesDto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static GameTypeRevisionResources? ParseModel(string? json)
    {
        var dto = ParseDto(json);
        if (dto is null)
        {
            return null;
        }

        return MapToModel(dto);
    }

    public static string? Serialize(GameTypeRevisionResourcesDto? resources)
    {
        if (resources is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(resources, Options);
    }

    public static string? Serialize(GameTypeRevisionResources? resources)
    {
        if (resources is null)
        {
            return null;
        }

        var dto = MapToDto(resources);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static GameTypeRevisionResources MapToModel(GameTypeRevisionResourcesDto dto)
    {
        return new GameTypeRevisionResources
        {
            CpuReservationCores = dto.CpuReservationCores,
            CpuLimitCores = dto.CpuLimitCores,
            MemoryReservationBytes = dto.MemoryReservationBytes,
            MemoryLimitBytes = dto.MemoryLimitBytes,
            MemoryReservationVariable = dto.MemoryReservationVariable,
            MemoryLimitVariable = dto.MemoryLimitVariable,
            PidsLimit = dto.PidsLimit,
            MaxReplicasPerNode = dto.MaxReplicasPerNode,
            Constraints = dto.Constraints.Select(c => new PlacementConstraint
            {
                Target = c.Target,
                Operator = c.Operator,
                Value = c.Value
            }).ToList(),
            Preferences = dto.Preferences.Select(p => new PlacementPreference
            {
                Strategy = p.Strategy,
                Descriptor = p.Descriptor
            }).ToList()
        };
    }

    public static GameTypeRevisionResourcesDto MapToDto(GameTypeRevisionResources model)
    {
        return new GameTypeRevisionResourcesDto
        {
            CpuReservationCores = model.CpuReservationCores,
            CpuLimitCores = model.CpuLimitCores,
            MemoryReservationBytes = model.MemoryReservationBytes,
            MemoryLimitBytes = model.MemoryLimitBytes,
            MemoryReservationVariable = model.MemoryReservationVariable,
            MemoryLimitVariable = model.MemoryLimitVariable,
            PidsLimit = model.PidsLimit,
            MaxReplicasPerNode = model.MaxReplicasPerNode,
            Constraints = model.Constraints.Select(c => new PlacementConstraintDto
            {
                Target = c.Target,
                Operator = c.Operator,
                Value = c.Value
            }).ToList(),
            Preferences = model.Preferences.Select(p => new PlacementPreferenceDto
            {
                Strategy = p.Strategy,
                Descriptor = p.Descriptor
            }).ToList()
        };
    }
}
