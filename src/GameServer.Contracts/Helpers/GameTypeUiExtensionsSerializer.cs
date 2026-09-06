using System.Text.Json;
using GameServer.API.Dtos.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Helpers for reading the JSON descriptor list on <c>GameTypeRevision.UiExtensionsJson</c>.
/// The GUI still enforces its own assembly whitelist before actually rendering these.
/// </summary>
public static class GameTypeUiExtensionsSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<GameTypeUiExtensionDescriptorDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var descriptors = JsonSerializer.Deserialize<List<GameTypeUiExtensionDescriptorDto>>(json, Options);
            return descriptors ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
