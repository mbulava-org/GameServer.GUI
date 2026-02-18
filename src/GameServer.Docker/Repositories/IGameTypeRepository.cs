using GameServer.Docker.Models;

namespace GameServer.Docker.Repositories
{
    /// <summary>
    /// Repository interface for GameType data access
    /// </summary>
    public interface IGameTypeRepository
    {
        // Initialization
        Task InitializeDatabaseAsync();
        
        // Query methods
        Task<List<GameTypeDefinition>> GetAllAsync(bool includeInactive = false);
        Task<GameTypeDefinition?> GetByKeyAsync(string key);
        Task<GameTypeDefinition?> GetByIdAsync(int id);
        Task<List<GameTypeDefinition>> SearchAsync(string searchTerm);
        Task<List<GameTypeDefinition>> GetWithTTYEnabledAsync();
        Task<bool> ExistsAsync(string key);
        
        // CRUD operations
        Task<GameTypeDefinition> CreateAsync(GameTypeDefinition gameType);
        Task<GameTypeDefinition> UpdateAsync(GameTypeDefinition gameType);
        Task DeleteAsync(string key);
        
        // Extended metadata operations
        Task<GameTypeExtendedMetadata?> GetExtendedMetadataAsync(string gameTypeKey);
        Task<GameTypeExtendedMetadata> SaveExtendedMetadataAsync(string gameTypeKey, GameTypeExtendedMetadata metadata);
        Task DeleteExtendedMetadataAsync(string gameTypeKey);
        
        // Setting metadata operations
        Task<SettingMetadata?> GetSettingMetadataAsync(string gameTypeKey, string settingKey);
        Task<Dictionary<string, SettingMetadata>> GetAllSettingMetadataAsync(string gameTypeKey);
        Task UpdateSettingMetadataAsync(string gameTypeKey, string settingKey, SettingMetadata metadata);
        Task DeleteSettingMetadataAsync(string gameTypeKey, string settingKey);
    }
}
