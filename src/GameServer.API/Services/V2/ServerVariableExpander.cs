using System.Text;
using GameServer.API.Models.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Expands <c>{PropertyName}</c> tokens inside setting values whose GameType data type is
/// <see cref="ServerVariableDataType"/> and whose stored value is flagged with <see cref="EnabledPrefix"/>.
/// </summary>
public static class ServerVariableExpander
{
    /// <summary>
    /// GameType setting metadata data type that opts a setting into variable expansion.
    /// </summary>
    public const string ServerVariableDataType = "servervariable";

    /// <summary>
    /// Marker prefix stored with the setting value when expansion is enabled for a server.
    /// </summary>
    public const string EnabledPrefix = "@vars:";

    /// <summary>
    /// Marker prefix used to escape a literal value that would otherwise start with <see cref="EnabledPrefix"/>.
    /// </summary>
    public const string LiteralPrefix = "@literal:";

    /// <summary>
    /// Curated set of GameServer properties exposed as tokens.
    /// </summary>
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

    /// <summary>
    /// Splits a stored value into its expansion flag and raw text.
    /// </summary>
    public static (bool ExpandVariables, string? RawValue) Decode(string? storedValue)
    {
        if (storedValue is null)
        {
            return (false, null);
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

    /// <summary>
    /// Combines an expansion flag and raw text into the persisted value representation.
    /// </summary>
    public static string? Encode(bool expandVariables, string? rawValue)
    {
        if (expandVariables)
        {
            return EnabledPrefix + (rawValue ?? string.Empty);
        }

        if (rawValue is not null
            && (rawValue.StartsWith(EnabledPrefix, StringComparison.Ordinal)
                || rawValue.StartsWith(LiteralPrefix, StringComparison.Ordinal)))
        {
            return LiteralPrefix + rawValue;
        }

        return rawValue;
    }

    /// <summary>
    /// Resolves the effective runtime value for a single stored setting value.
    /// </summary>
    public static string? Resolve(string? storedValue, IReadOnlyDictionary<string, string?> tokenValues)
    {
        ArgumentNullException.ThrowIfNull(tokenValues);

        var (expandVariables, rawValue) = Decode(storedValue);

        return expandVariables ? Substitute(rawValue, tokenValues) : rawValue;
    }

    /// <summary>
    /// Builds the curated token dictionary for a server and its game type revision.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> BuildTokenValues(
        Models.V2.GameServer server,
        GameType? gameType,
        GameTypeRevision? revision)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerId"] = server.ServerId,
            ["Name"] = server.Name,
            ["ServiceName"] = server.ServiceName,
            ["Description"] = server.Description,
            ["Status"] = server.Status,
            ["GameTypeKey"] = gameType?.Key,
            ["RevisionVersionTag"] = revision?.VersionTag,
            ["RevisionImageReference"] = revision?.ImageReference
        };
    }

    /// <summary>
    /// Replaces <c>{Token}</c> occurrences with their values. Unknown tokens are left untouched.
    /// </summary>
    public static string? Substitute(string? value, IReadOnlyDictionary<string, string?> tokenValues)
    {
        ArgumentNullException.ThrowIfNull(tokenValues);

        if (string.IsNullOrEmpty(value) || !value.Contains('{', StringComparison.Ordinal))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var index = 0;

        while (index < value.Length)
        {
            var open = value.IndexOf('{', index);
            if (open < 0)
            {
                builder.Append(value, index, value.Length - index);
                break;
            }

            var close = value.IndexOf('}', open + 1);
            if (close < 0)
            {
                builder.Append(value, index, value.Length - index);
                break;
            }

            builder.Append(value, index, open - index);

            var token = value[(open + 1)..close];
            if (tokenValues.TryGetValue(token, out var replacement))
            {
                builder.Append(replacement ?? string.Empty);
            }
            else
            {
                builder.Append(value, open, close - open + 1);
            }

            index = close + 1;
        }

        return builder.ToString();
    }
}
