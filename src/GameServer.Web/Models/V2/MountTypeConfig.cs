namespace GameServer.Web.Models.V2;

public sealed record MountTypeConfig
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Template used to compute the name of the Docker named volume created for this mount type.
    /// Must resolve to a value that is unique per server/volume combination.
    /// </summary>
    public string? VolumeNameFormat { get; set; }

    /// <summary>
    /// Free-form key/value options for this mount type.
    /// Driver, DriverOptionsJson, SourcePathTemplate, DefaultReadOnly, DefaultEnsureNfsPathExists,
    /// DefaultOwnerUid, DefaultOwnerGid, DefaultPermissions.
    /// </summary>
    public Dictionary<string, string>? Options { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

