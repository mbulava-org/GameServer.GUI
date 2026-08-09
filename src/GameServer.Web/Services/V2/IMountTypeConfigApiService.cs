using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

/// <summary>
/// Web-side client abstraction for the mount-type configuration API.
/// </summary>
public interface IMountTypeConfigApiService
{
    /// <summary>
    /// Gets all mount-type configurations.
    /// </summary>
    Task<IReadOnlyList<MountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single mount-type configuration by key.
    /// </summary>
    Task<MountTypeConfig> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a mount-type configuration.
    /// </summary>
    Task<MountTypeConfig> SaveAsync(MountTypeConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a mount-type configuration.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
