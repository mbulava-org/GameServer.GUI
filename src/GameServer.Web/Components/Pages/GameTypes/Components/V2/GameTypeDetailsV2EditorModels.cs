namespace GameServer.Web.Components.Pages.GameTypes.Components.V2;

using GameServer.Web.Models.V2;

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
