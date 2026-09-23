using System.Text.Json;
using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Helpers for serializing and deserializing <see cref="GameTypeRevisionHealthcheckDto"/> and <see cref="GameTypeRevisionHealthcheck"/>.
/// </summary>
public static class GameTypeHealthcheckSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static GameTypeRevisionHealthcheckDto? ParseDto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GameTypeRevisionHealthcheckDto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static GameTypeRevisionHealthcheck? ParseModel(string? json)
    {
        var dto = ParseDto(json);
        if (dto is null)
        {
            return null;
        }

        return MapToModel(dto);
    }

    public static string? Serialize(GameTypeRevisionHealthcheckDto? healthcheck)
    {
        if (healthcheck is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(healthcheck, Options);
    }

    public static string? Serialize(GameTypeRevisionHealthcheck? healthcheck)
    {
        if (healthcheck is null)
        {
            return null;
        }

        var dto = MapToDto(healthcheck);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static GameTypeRevisionHealthcheck MapToModel(GameTypeRevisionHealthcheckDto dto)
    {
        return new GameTypeRevisionHealthcheck
        {
            Disable = dto.Disable,
            TestType = dto.TestType,
            TestCommand = dto.TestCommand,
            IntervalSeconds = dto.IntervalSeconds,
            TimeoutSeconds = dto.TimeoutSeconds,
            StartPeriodSeconds = dto.StartPeriodSeconds,
            StartIntervalSeconds = dto.StartIntervalSeconds,
            Retries = dto.Retries
        };
    }

    public static GameTypeRevisionHealthcheckDto MapToDto(GameTypeRevisionHealthcheck model)
    {
        return new GameTypeRevisionHealthcheckDto
        {
            Disable = model.Disable,
            TestType = model.TestType,
            TestCommand = model.TestCommand,
            IntervalSeconds = model.IntervalSeconds,
            TimeoutSeconds = model.TimeoutSeconds,
            StartPeriodSeconds = model.StartPeriodSeconds,
            StartIntervalSeconds = model.StartIntervalSeconds,
            Retries = model.Retries
        };
    }
}
