namespace GameServer.Web.Models.V2;

public sealed record MountTypeConfig
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Driver { get; set; } = string.Empty;

    public string? DriverOptionsJson { get; set; }

    public string SourcePathTemplate { get; set; } = string.Empty;

    public string ContainerPathTemplate { get; set; } = "{Source}";

    public bool DefaultReadOnly { get; set; }

    public string DefaultInitMode { get; set; } = "none";

    public int? DefaultOwnerUid { get; set; }

    public int? DefaultOwnerGid { get; set; }

    public string? DefaultPermissions { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
