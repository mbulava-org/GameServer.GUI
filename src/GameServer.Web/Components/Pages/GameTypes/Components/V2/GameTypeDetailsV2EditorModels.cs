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

    public string MountType { get; set; } = "volume";

    public bool ReadOnly { get; set; }

    public int? OwnerUid { get; set; }

    public int? OwnerGid { get; set; }

    public string? OwnerUidVariable { get; set; }

    public string? OwnerGidVariable { get; set; }

    public string? Permissions { get; set; }

    public bool EnsureNfsPathExists { get; set; }

    public bool Required { get; set; } = true;
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

    /// <summary>
    /// The underlying data type for enum values (string or numeric).
    /// Used by the enum editor UI; not persisted directly.
    /// </summary>
    public string EnumUnderlyingType { get; set; } = "string";

    /// <summary>
    /// Editable enum value entries. Parsed from AllowedValuesJson/ValueMappingsJson on load,
    /// and synced back to those JSON fields on change.
    /// </summary>
    public List<EnumValueDraft> EnumValues { get; set; } = [];

    public List<GameTypeRevisionPortMappingDraft> PortMappings { get; set; } = [];
}

public sealed class EnumValueDraft
{
    /// <summary>
    /// The actual value sent to the server/container (e.g. "0", "survival", "1").
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The user-friendly display label shown in the UI (e.g. "Survival", "Creative").
    /// </summary>
    public string DisplayLabel { get; set; } = string.Empty;
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

public sealed class VolumeMountTypeOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool DefaultReadOnly { get; init; }

    public bool DefaultEnsureNfsPathExists { get; init; }

    public int? DefaultOwnerUid { get; init; }

    public int? DefaultOwnerGid { get; init; }

    public string? DefaultPermissions { get; init; }
}

public sealed class VolumeNumericVariableOption
{
    public string SettingKey { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int? DefaultValue { get; init; }

    public string? DataType { get; init; }
}

public static partial class GameTypeRevisionWebHostDraftRules
{
    public static IReadOnlyList<string> SupportedPathVariables { get; } = ["serverId", "name", "serviceName", "gameType"];

    public static string BuildPathSegmentFromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var slug = NonPathCharacterRegex().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return MultiDashRegex().Replace(slug, "-");
    }

    public static List<string> GetPathSegmentValidationIssues(string? pathSegment)
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

public sealed class GameTypeRevisionUiExtensionDraft
{
    public string Title { get; set; } = string.Empty;

    public string ComponentTypeName { get; set; } = string.Empty;

    public string? AssemblyName { get; set; }

    public string? Icon { get; set; }

    public int Order { get; set; }

    public List<GameTypeRevisionUiExtensionParameterDraft> Parameters { get; set; } = new();
}

public sealed class GameTypeRevisionUiExtensionParameterDraft
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class GameTypeRevisionResourcesDraft
{
    public decimal? CpuReservationCores { get; set; }

    public decimal? CpuLimitCores { get; set; }

    public decimal? MemoryReservationValue { get; set; }

    public string MemoryReservationUnit { get; set; } = "MB";

    public string? MemoryReservationVariable { get; set; }

    public decimal? MemoryLimitValue { get; set; }

    public string MemoryLimitUnit { get; set; } = "MB";

    public string? MemoryLimitVariable { get; set; }

    public long? PidsLimit { get; set; }

    public ulong? MaxReplicasPerNode { get; set; }

    public List<PlacementConstraintDraft> Constraints { get; set; } = [];

    public List<PlacementPreferenceDraft> Preferences { get; set; } = [];

    public long? GetMemoryReservationBytes() => GameTypeRevisionResourcesDraftRules.ConvertToBytes(MemoryReservationValue, MemoryReservationUnit);

    public long? GetMemoryLimitBytes() => GameTypeRevisionResourcesDraftRules.ConvertToBytes(MemoryLimitValue, MemoryLimitUnit);

    public GameTypeRevisionResourcesDraft Clone()
    {
        return new GameTypeRevisionResourcesDraft
        {
            CpuReservationCores = CpuReservationCores,
            CpuLimitCores = CpuLimitCores,
            MemoryReservationValue = MemoryReservationValue,
            MemoryReservationUnit = MemoryReservationUnit,
            MemoryReservationVariable = MemoryReservationVariable,
            MemoryLimitValue = MemoryLimitValue,
            MemoryLimitUnit = MemoryLimitUnit,
            MemoryLimitVariable = MemoryLimitVariable,
            PidsLimit = PidsLimit,
            MaxReplicasPerNode = MaxReplicasPerNode,
            Constraints = Constraints.Select(c => c.Clone()).ToList(),
            Preferences = Preferences.Select(p => p.Clone()).ToList()
        };
    }
}

public sealed class PlacementConstraintDraft
{
    public string TargetType { get; set; } = "node.role";

    public string CustomTarget { get; set; } = string.Empty;

    public string Operator { get; set; } = "==";

    public string Value { get; set; } = "worker";

    public string GetComputedTarget()
    {
        return TargetType switch
        {
            "custom" => CustomTarget.Trim(),
            _ => TargetType
        };
    }

    public string GetComputedExpression()
    {
        var target = GetComputedTarget();
        if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(Value))
        {
            return string.Empty;
        }

        return $"{target} {Operator} {Value}".Trim();
    }

    public PlacementConstraintDraft Clone()
    {
        return new PlacementConstraintDraft
        {
            TargetType = TargetType,
            CustomTarget = CustomTarget,
            Operator = Operator,
            Value = Value
        };
    }
}

public sealed class PlacementPreferenceDraft
{
    public string Strategy { get; set; } = "spread";

    public string LabelKey { get; set; } = "node.labels.zone";

    public PlacementPreferenceDraft Clone()
    {
        return new PlacementPreferenceDraft
        {
            Strategy = Strategy,
            LabelKey = LabelKey
        };
    }
}

public sealed class ResourcePresetOption
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Icon { get; init; } = "memory";

    public decimal? CpuReservation { get; init; }

    public decimal? CpuLimit { get; init; }

    public decimal? MemoryReservationValue { get; init; }

    public string MemoryReservationUnit { get; init; } = "MB";

    public decimal? MemoryLimitValue { get; init; }

    public string MemoryLimitUnit { get; init; } = "MB";

    public long? PidsLimit { get; init; }

    public string? SuggestedConstraint { get; init; }
}

public static class GameTypeRevisionResourcesDraftRules
{
    public static readonly IReadOnlyList<string> MemoryUnits = ["MB", "GB"];

    public static readonly IReadOnlyList<string> ConstraintOperators = ["==", "!="];

    public static readonly IReadOnlyList<string> CommonConstraintTargets =
    [
        "node.role",
        "node.hostname",
        "node.id",
        "node.labels.environment",
        "node.labels.zone",
        "node.labels.datacenter",
        "node.labels.hardware",
        "engine.labels.operatingsystem",
        "custom"
    ];

    public static readonly IReadOnlyList<ResourcePresetOption> Presets =
    [
        new()
        {
            Name = "Light (1 Core, 2 GB)",
            Description = "Basic lightweight server (e.g. Terraria, small Minecraft)",
            Icon = "eco",
            CpuReservation = 0.5m,
            CpuLimit = 1.0m,
            MemoryReservationValue = 1024,
            MemoryReservationUnit = "MB",
            MemoryLimitValue = 2048,
            MemoryLimitUnit = "MB",
            PidsLimit = 200
        },
        new()
        {
            Name = "Standard (2 Cores, 4 GB)",
            Description = "Standard multiplayer game server (e.g. Valheim, 7 Days to Die)",
            Icon = "sports_esports",
            CpuReservation = 1.0m,
            CpuLimit = 2.0m,
            MemoryReservationValue = 2048,
            MemoryReservationUnit = "MB",
            MemoryLimitValue = 4096,
            MemoryLimitUnit = "MB",
            PidsLimit = 500
        },
        new()
        {
            Name = "High Performance (4 Cores, 8 GB)",
            Description = "Demanding game server (e.g. Palworld, Ark, heavy modpacks)",
            Icon = "bolt",
            CpuReservation = 2.0m,
            CpuLimit = 4.0m,
            MemoryReservationValue = 4096,
            MemoryReservationUnit = "MB",
            MemoryLimitValue = 8192,
            MemoryLimitUnit = "MB",
            PidsLimit = 1000
        },
        new()
        {
            Name = "Dedicated Node (8 Cores, 16 GB)",
            Description = "Full node dedicated instance with worker node constraint",
            Icon = "dns",
            CpuReservation = 4.0m,
            CpuLimit = 8.0m,
            MemoryReservationValue = 8192,
            MemoryReservationUnit = "MB",
            MemoryLimitValue = 16384,
            MemoryLimitUnit = "MB",
            PidsLimit = 2000,
            SuggestedConstraint = "node.role == worker"
        }
    ];

    public static long? ConvertToBytes(decimal? value, string unit)
    {
        if (value is null or <= 0)
        {
            return null;
        }

        const long oneMb = 1024L * 1024L;
        const long oneGb = 1024L * 1024L * 1024L;

        return string.Equals(unit, "GB", StringComparison.OrdinalIgnoreCase)
            ? (long)Math.Round(value.Value * oneGb)
            : (long)Math.Round(value.Value * oneMb);
    }

    public static (decimal Value, string Unit) ConvertFromBytes(long bytes)
    {
        const long oneGb = 1024L * 1024L * 1024L;
        const long oneMb = 1024L * 1024L;

        if (bytes >= oneGb && bytes % oneGb == 0)
        {
            return (bytes / (decimal)oneGb, "GB");
        }

        return (bytes / (decimal)oneMb, "MB");
    }

    public static List<string> ValidateResources(GameTypeRevisionResourcesDraft? draft)
    {
        var issues = new List<string>();
        if (draft is null)
        {
            return issues;
        }

        if (draft.CpuReservationCores is < 0)
        {
            issues.Add("CPU reservation cannot be negative.");
        }

        if (draft.CpuLimitCores is < 0)
        {
            issues.Add("CPU limit cannot be negative.");
        }

        if (draft.CpuReservationCores.HasValue && draft.CpuLimitCores.HasValue
            && draft.CpuReservationCores.Value > draft.CpuLimitCores.Value)
        {
            issues.Add($"CPU reservation ({draft.CpuReservationCores.Value:0.##} cores) cannot exceed CPU limit ({draft.CpuLimitCores.Value:0.##} cores).");
        }

        var memoryReservationBytes = draft.GetMemoryReservationBytes();
        var memoryLimitBytes = draft.GetMemoryLimitBytes();

        if (draft.MemoryReservationValue is < 0)
        {
            issues.Add("Memory reservation cannot be negative.");
        }

        if (draft.MemoryLimitValue is < 0)
        {
            issues.Add("Memory limit cannot be negative.");
        }

        if (memoryReservationBytes.HasValue && memoryLimitBytes.HasValue
            && memoryReservationBytes.Value > memoryLimitBytes.Value)
        {
            issues.Add($"Memory reservation ({draft.MemoryReservationValue} {draft.MemoryReservationUnit}) cannot exceed Memory limit ({draft.MemoryLimitValue} {draft.MemoryLimitUnit}).");
        }

        if (draft.PidsLimit is < 0)
        {
            issues.Add("PIDs limit cannot be negative.");
        }

        foreach (var (constraint, index) in draft.Constraints.Select((c, i) => (c, i + 1)))
        {
            var target = constraint.GetComputedTarget();
            if (string.IsNullOrWhiteSpace(target))
            {
                issues.Add($"Constraint #{index}: Target is required.");
            }

            if (string.IsNullOrWhiteSpace(constraint.Value))
            {
                issues.Add($"Constraint #{index}: Value is required.");
            }
        }

        foreach (var (pref, index) in draft.Preferences.Select((p, i) => (p, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(pref.LabelKey))
            {
                issues.Add($"Placement preference #{index}: Label key is required.");
            }
        }

        return issues;
    }
}
