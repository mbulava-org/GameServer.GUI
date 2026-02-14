using GameServer.Docker.Models;

namespace GameServer.Docker.Interfaces
{
    public interface IGameTypeRegistry
    {
        public Task<List<GameTypeDefinition>> GetAll();

        public Task<GameTypeDefinition?> Get(string key);

        public Task AddOrUpdate(GameTypeDefinition def);

        public Task Delete(string key);
    }
}
