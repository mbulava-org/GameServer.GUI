using GameServer.Docker.Models;

namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// OBSOLETE: File-based game type registry. Use IGameTypeRepository instead for database-backed storage.
    /// </summary>
    [Obsolete("IGameTypeRegistry is obsolete. Use IGameTypeRepository from GameServer.Docker.Repositories instead. This file-based registry will be removed in a future version.")]
    public interface IGameTypeRegistry
    {
        public Task<List<GameTypeDefinition>> GetAll();

        public Task<GameTypeDefinition?> Get(string key);

        public Task AddOrUpdate(GameTypeDefinition def);

        public Task Delete(string key);
    }
}
