using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServer.Web.Models.V2;

/// <summary>
/// UI-side encoding helpers for the "Server Varible (Optional)" setting data type.
/// Mirrors <c>GameServer.API.Services.V2.ServerVariableExpander</c>, which performs the
/// actual token substitution at deployment time.
/// </summary>
public static class ServerVariableSetting
{
    public const string DataType = "servervariable";

    public const string EnabledPrefix = "@vars:";

    public const string LiteralPrefix = "@literal:";

    public const string DefinitionDefaultPrefix = "@svdef:";

    public static IReadOnlyList<string> SupportedTokens { get; } =
    [
        "ServerId",
        "Name",
        "ServiceName",
        "Description",
        "Status",
        "GameTypeKey",
        "RevisionVersionTag",
        "RevisionImageReference"
    ];

    public static (bool ExpandVariables, string? RawValue) Decode(string? storedValue)
    {
        if (storedValue is null)
        {
            return (false, null);
        }

        if (storedValue.StartsWith(DefinitionDefaultPrefix, StringComparison.Ordinal))
        {
            var (defEnabled, onVal, offVal) = DecodeDefinitionDefault(storedValue);
            return (defEnabled, defEnabled ? onVal : offVal);
        }

        if (storedValue.StartsWith(EnabledPrefix, StringComparison.Ordinal))
        {
            return (true, storedValue[EnabledPrefix.Length..]);
        }

        if (storedValue.StartsWith(LiteralPrefix, StringComparison.Ordinal))
        {
            return (false, storedValue[LiteralPrefix.Length..]);
        }

        return (false, storedValue);
    }

    public static string? Encode(bool expandVariables, string? rawValue)
    {
        if (expandVariables)
        {
            return EnabledPrefix + (rawValue ?? string.Empty);
        }

        if (rawValue is not null
            && (rawValue.StartsWith(EnabledPrefix, StringComparison.Ordinal)
                || rawValue.StartsWith(LiteralPrefix, StringComparison.Ordinal)
                || rawValue.StartsWith(DefinitionDefaultPrefix, StringComparison.Ordinal)))
        {
            return LiteralPrefix + rawValue;
        }

        return rawValue;
    }

    public static (bool DefaultEnabled, string? OnValue, string? OffValue) DecodeDefinitionDefault(string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return (false, null, null);
        }

        if (defaultValue.StartsWith(DefinitionDefaultPrefix, StringComparison.Ordinal))
        {
            var json = defaultValue[DefinitionDefaultPrefix.Length..];
            try
            {
                var payload = JsonSerializer.Deserialize<ServerVariableDefinitionDefaultPayload>(json);
                if (payload is not null)
                {
                    return (payload.Enabled, payload.On, payload.Off);
                }
            }
            catch (JsonException)
            {
                // Fallback on malformed json
            }
        }

        if (defaultValue.StartsWith(EnabledPrefix, StringComparison.Ordinal))
        {
            return (true, defaultValue[EnabledPrefix.Length..], null);
        }

        if (defaultValue.StartsWith(LiteralPrefix, StringComparison.Ordinal))
        {
            return (false, null, defaultValue[LiteralPrefix.Length..]);
        }

        return (false, defaultValue, defaultValue);
    }

    public static string EncodeDefinitionDefault(bool defaultEnabled, string? onValue, string? offValue)
    {
        var payload = new ServerVariableDefinitionDefaultPayload
        {
            Enabled = defaultEnabled,
            On = onValue,
            Off = offValue
        };

        return DefinitionDefaultPrefix + JsonSerializer.Serialize(payload);
    }

    private sealed class ServerVariableDefinitionDefaultPayload
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("on")]
        public string? On { get; set; }

        [JsonPropertyName("off")]
        public string? Off { get; set; }
    }
}
