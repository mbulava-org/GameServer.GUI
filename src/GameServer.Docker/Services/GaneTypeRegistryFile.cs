using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GameServer.Docker.Services
{
    public class GaneTypeRegistryFile : IGameTypeRegistry
    {
        private readonly GameTypeRegistryData _fileOptions;
        private Dictionary<string, GameTypeDefinition> _definitions = new();
        private SemaphoreSlim saveLock = new SemaphoreSlim(1, 1);  
        private readonly ILogger<GaneTypeRegistryFile> _logger;

        public GaneTypeRegistryFile(ILogger<GaneTypeRegistryFile> logger,
            IOptions<Configurations.GameTypeRegistryData> options) 
        {
            _logger = logger;
            _fileOptions = options.Value;
            
            if (_fileOptions == null)
                throw new ArgumentNullException(nameof(options), "GameTypeRegistryData options cannot be null.");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_fileOptions.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created GameTypeRegistry directory: {Directory}", directory);
            }

            if (File.Exists(_fileOptions.FilePath))
            {
                // Load existing file
                LoadData().Wait();
                _logger.LogInformation("GameTypeRegistry loaded from existing file with {Count} game type(s)", _definitions.Count);
            }
            else
            {
                // File doesn't exist - start with empty registry
                _logger.LogInformation("GameTypeRegistry file not found. Starting with empty registry. Add game types via API.");
            }
        }

        private async Task SaveData()
        {
            _logger.LogInformation("Attempting to save GameTypeRegistryData to file.");
            await saveLock.WaitAsync();
            try
            {
                _logger.LogInformation("Saving GameTypeRegistryData to file: {path}", _fileOptions.FilePath);
                
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(_fileOptions.FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Use FileMode.Create to truncate and overwrite the file completely
                using (FileStream fs = new FileStream(_fileOptions.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await JsonSerializer.SerializeAsync<Dictionary<string, GameTypeDefinition>>(fs, _definitions, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    // Ensure all data is written to disk before releasing lock
                    await fs.FlushAsync();
                }
                _logger.LogInformation("GameTypeRegistryData saved successfully.");
            }
            finally
            {
                saveLock.Release();
            }
        }

        private async Task LoadData()
        {
            _logger.LogInformation("Attempting to load GameTypeRegistryData from file.");
            await saveLock.WaitAsync();
            try
            {
                _logger.LogInformation("Loading GameTypeRegistryData from file: {path}", _fileOptions.FilePath);
                using(FileStream fs = new(_fileOptions.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, GameTypeDefinition>>(fs);
                    if(loaded == null)
                    {
                        _logger.LogWarning("No GameTypeRegistryData found in file, initializing empty registry.");
                    }
                    _definitions = loaded ?? new Dictionary<string, GameTypeDefinition>();

                }
                    
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error loading GameTypeRegistryData from file.");
                throw;
            }
            finally
            {
                saveLock.Release();
            }
        }

        public async Task AddOrUpdate(GameTypeDefinition def)
        {
            var isNew = !_definitions.ContainsKey(def.Key);
            _definitions[def.Key] = def;
            
            await SaveData();
            _logger.LogInformation("{Action} game type: {GameType}", isNew ? "Created" : "Updated", def.Key);
        }

        public async Task Delete(string key)
        {
            if (_definitions.Remove(key))
            {
                await SaveData();
                _logger.LogInformation("Deleted game type: {GameType}", key);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent game type: {GameType}", key);
            }
        }

        public Task<GameTypeDefinition?> Get(string key)
        {
            //if (!_definitions.ContainsKey(key))
            //{
            //    return Task.FromResult<GameTypeDefinition?>(null);
            //}
            return Task.FromResult<GameTypeDefinition?>(_definitions[key]);
        }

        public Task<List<GameTypeDefinition>> GetAll()
        {
            return _definitions.Values.ToList() is List<GameTypeDefinition> list
                ? Task.FromResult(list)
                : Task.FromResult(new List<GameTypeDefinition>());
        }
    }
}
