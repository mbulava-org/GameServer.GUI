namespace GameServer.Web.Models.V2;

/// <summary>
/// UI-side encoding helpers for the "Server Varible (Optional)" setting data type.
/// Mirrors <c>GameServer.Docker.Services.V2.ServerVariableExpander</c>, which performs the
/// actual token substitution at deployment time.
/// </summary>
public static class ServerVariableSetting
{
    public const string DataType = "servervariable";

    public const string EnabledPrefix = "@vars:";

    public const string LiteralPrefix = "@literal:";

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
                || rawValue.StartsWith(LiteralPrefix, StringComparison.Ordinal)))
        {
            return LiteralPrefix + rawValue;
        }

        return rawValue;
    }
}
