namespace GameServer.Web.Components.Pages.GameTypes.Components.V2;

using GameServer.Web.Models.V2;
using System.Text.RegularExpressions;

public sealed class GameTypeRevisionListRow
{
    public int? Id { get; set; }

    public string VersionTag { get; set; } = string.Empty;

    public string? ImageDigest { get; set; }

    public bool IsPublished { get; set; }

    public bool EnableTTY { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsUnsavedDraft { get; set; }

    public GameTypeRevision? SourceRevision { get; set; }
}

public sealed class GameTypeRevisionPortDraft
{
    public int ContainerPort { get; set; }

    public string Protocol { get; set; } = "tcp";

    public bool AdvertisedPort { get; set; }

    public string? Description { get; set; }
}

public sealed class GameTypeRevisionVolumeDraft
{
    public string Source { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Usage { get; set; } = "config";
}

public sealed class GameTypeRevisionSettingDraft
{
    public string DraftId { get; set; } = Guid.NewGuid().ToString("N");

    public string SettingKey { get; set; } = string.Empty;

    public string? DefaultValue { get; set; }

    public string? Description { get; set; }

    public GameTypeRevisionSettingMetadataDraft Metadata { get; set; } = new();
}

public sealed class GameTypeRevisionSettingMetadataDraft
{
    public string? DataType { get; set; }

    public string? Category { get; set; }

    public bool IsRequired { get; set; }

    public bool CannotBeEmpty { get; set; }

    public string? Placeholder { get; set; }

    public string? ValidationPattern { get; set; }

    public string? ValidationMessage { get; set; }

    public bool AutoAllocatePort { get; set; }

    public bool ValidateRelatedPortsAvailability { get; set; }

    public string? AllowedValuesJson { get; set; }

    public string? ValueMappingsJson { get; set; }

    public List<GameTypeRevisionPortMappingDraft> PortMappings { get; set; } = [];
}

public sealed class GameTypeRevisionPortMappingDraft
{
    public string MappingRole { get; set; } = nameof(GameTypeSettingPortMappingRole.Primary);

    public string RelationType { get; set; } = nameof(GameTypeSettingPortRelationType.Direct);

    public int TargetContainerPort { get; set; }

    public string TargetProtocol { get; set; } = "tcp";

    public int? CalculationValue { get; set; }

    public bool IsRequired { get; set; }
}

public sealed class GameTypeRevisionWebHostDraft
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? PathSegment { get; set; }

    public int? ContainerPort { get; set; }

    public string? ContainerPortVariable { get; set; }

    public string? EnabledWhen { get; set; }
}

public sealed class WebHostPortVariableOption
{
    public string SettingKey { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int? DefaultPort { get; init; }

    public string? DataType { get; init; }

    public bool IsCompatible { get; init; } = true;
}

internal static partial class GameTypeRevisionWebHostDraftRules
{
    internal static IReadOnlyList<string> SupportedPathVariables { get; } = ["serverId", "name", "serviceName", "gameType"];

    internal static string BuildPathSegmentFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var slug = NonPathCharacterRegex().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return MultiDashRegex().Replace(slug, "-");
    }

    internal static List<string> GetPathSegmentValidationIssues(string? pathSegment)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(pathSegment))
        {
            return issues;
        }

        if (!string.Equals(pathSegment, pathSegment.Trim(), StringComparison.Ordinal))
        {
            issues.Add("path segment cannot start or end with whitespace.");
        }

        var trimmedPathSegment = pathSegment.Trim();
        if (trimmedPathSegment.StartsWith('/') || trimmedPathSegment.EndsWith('/'))
        {
            issues.Add("path segment cannot start or end with '/'. Use a relative path segment only.");
        }

        if (trimmedPathSegment.Contains("//", StringComparison.Ordinal))
        {
            issues.Add("path segment cannot contain empty path segments ('//').");
        }

        foreach (Match match in PathVariableRegex().Matches(trimmedPathSegment))
        {
            var variableName = match.Groups["name"].Value;
            if (!SupportedPathVariables.Contains(variableName, StringComparer.OrdinalIgnoreCase))
            {
                var supportedVariables = string.Join(", ", SupportedPathVariables.Select(variable => $"{{{variable}}}"));
                issues.Add($"path segment uses unsupported runtime variable '{{{variableName}}}'. Supported variables: {supportedVariables}.");
            }
        }

        var literalContent = PathVariableRegex().Replace(trimmedPathSegment, string.Empty);
        if (literalContent.Contains('{') || literalContent.Contains('}'))
        {
            issues.Add("path segment contains malformed runtime variable placeholders. Use values like '{serverId}'.");
        }

        if (!LiteralPathCharacterRegex().IsMatch(literalContent))
        {
            issues.Add("path segment can only contain lowercase letters, numbers, hyphens, forward slashes, and supported runtime variables.");
        }

        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex("[^a-z0-9/{}-]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonPathCharacterRegex();

    [GeneratedRegex("-{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiDashRegex();

    [GeneratedRegex("\\{(?<name>[A-Za-z][A-Za-z0-9]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PathVariableRegex();

    [GeneratedRegex("^[a-z0-9/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralPathCharacterRegex();
}
