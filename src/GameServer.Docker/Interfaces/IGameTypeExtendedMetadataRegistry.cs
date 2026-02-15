using GameServer.Docker.Models;

namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// OBSOLETE: File-based extended metadata registry. Use IGameTypeRepository.GetExtendedMetadataAsync() and related methods instead.
    /// </summary>
    [Obsolete("IGameTypeExtendedMetadataRegistry is obsolete. Use IGameTypeRepository from GameServer.Docker.Repositories for extended metadata operations. This file-based registry will be removed in a future version.")]
    public interface IGameTypeExtendedMetadataRegistry
    {
        /// <summary>
        /// Gets all extended metadata entries
        /// </summary>
        Task<List<GameTypeExtendedMetadata>> GetAll();

        /// <summary>
        /// Gets extended metadata for a specific game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        Task<GameTypeExtendedMetadata?> Get(string gameTypeKey);

        /// <summary>
        /// Adds or updates extended metadata for a game type
        /// </summary>
        /// <param name="metadata">The metadata to add or update</param>
        Task AddOrUpdate(GameTypeExtendedMetadata metadata);

        /// <summary>
        /// Deletes extended metadata for a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        Task Delete(string gameTypeKey);
    }
}
