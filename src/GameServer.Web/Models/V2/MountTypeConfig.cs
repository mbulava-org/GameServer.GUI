namespace GameServer.Web.Models.V2;

public sealed record MountTypeConfig
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Free-form key/value options for this mount type. Well-known keys include:
    /// Driver, DriverOptionsJson, SourcePathTemplate, DefaultReadOnly, DefaultEnsureNfsPathExists,
    /// DefaultOwnerUid, DefaultOwnerGid, DefaultPermissions.
    /// </summary>
    public Dictionary<string, string>? Options { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

