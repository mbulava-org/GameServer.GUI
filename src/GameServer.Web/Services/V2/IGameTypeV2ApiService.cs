using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

/// <summary>
/// Web-side client abstraction for the V2 GameType API.
/// </summary>
public interface IGameTypeV2ApiService
{
    /// <summary>
    /// Gets the GameType list.
    /// </summary>
    Task<IReadOnlyList<GameTypeListItem>> GetListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a GameType by key.
    /// </summary>
    Task<GameTypeDetail?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a GameType.
    /// </summary>
    Task<GameTypeDetail> CreateAsync(SaveGameTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a GameType.
    /// </summary>
    Task<GameTypeDetail> UpdateAsync(string key, SaveGameTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a GameType.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a GameType as a portable package.
    /// </summary>
    Task<PortableGameTypePackage> ExportAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a portable GameType package.
    /// </summary>
    Task<GameTypeDetail> ImportAsync(PortableGameTypePackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a revision to a GameType.
    /// </summary>
    Task<GameTypeRevision> AddRevisionAsync(string key, SaveGameTypeRevisionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing GameType revision.
    /// </summary>
    Task<GameTypeRevision> UpdateRevisionAsync(string key, int revisionId, SaveGameTypeRevisionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a GameType revision.
    /// </summary>
    Task<GameTypeRevision> PublishRevisionAsync(string key, int revisionId, bool setAsCurrentRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current revision for a GameType.
    /// </summary>
    Task SetCurrentRevisionAsync(string key, int revisionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects setup metadata for an image reference.
    /// </summary>
    Task<GameTypeSetupDetectionResult> DetectSetupAsync(string imageReference, string? versionTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects setup metadata for an image reference in the context of an existing GameType.
    /// </summary>
    Task<GameTypeSetupDetectionResult> DetectSetupAsync(string key, string imageReference, string? versionTag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares detected setup metadata against an existing revision.
    /// </summary>
    Task<GameTypeSetupComparisonResult> CompareSetupAsync(string key, string imageReference, string? versionTag, int revisionId, CancellationToken cancellationToken = default);
}
