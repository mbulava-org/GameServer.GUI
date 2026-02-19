using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// OBSOLETE: File-based registry for GameType extended metadata. Use GameTypeRepository with database storage instead.
    /// Each game type is stored in its own file: {DirectoryPath}/{GameTypeKey}.json
    /// </summary>
    [Obsolete("GameTypeExtendedMetadataRegistryFile is obsolete. Use GameTypeRepository from GameServer.Docker.Repositories for extended metadata operations. This file-based implementation will be removed in a future version.")]
#pragma warning disable CS0618 // Type or member is obsolete
    public class GameTypeExtendedMetadataRegistryFile : IGameTypeExtendedMetadataRegistry
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private readonly GameTypeExtendedMetadataRegistryData _fileOptions;
        private Dictionary<string, GameTypeExtendedMetadata> _metadata = new();
        private SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<GameTypeExtendedMetadataRegistryFile> _logger;
        private readonly string _directoryPath;

        public GameTypeExtendedMetadataRegistryFile(
            ILogger<GameTypeExtendedMetadataRegistryFile> logger,
            IOptions<GameTypeExtendedMetadataRegistryData> options)
        {
            _logger = logger;
            _fileOptions = options.Value;
            
            if (_fileOptions == null)
                throw new ArgumentNullException(nameof(options), "GameTypeExtendedMetadataRegistryData options cannot be null.");

            _directoryPath = _fileOptions.DirectoryPath;

            // Ensure directory exists
            if (!Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
                _logger.LogInformation("Created GameTypeExtendedMetadata directory: {DirectoryPath}", _directoryPath);
            }

            // Load all existing metadata files
            LoadAllMetadataFiles();

            _logger.LogInformation("GameTypeExtendedMetadata registry initialized with {Count} game type(s)", _metadata.Count);
        }

        /// <summary>
        /// Gets the file path for a specific game type
        /// </summary>
        private string GetFilePathForGameType(string gameTypeKey)
        {
            // Sanitize the game type key to ensure it's safe for use as a filename
            var sanitizedKey = string.Concat(gameTypeKey.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            return Path.Combine(_directoryPath, $"{sanitizedKey}.json");
        }

        /// <summary>
        /// Loads all metadata files from the directory
        /// </summary>
        private void LoadAllMetadataFiles()
        {
            if (!Directory.Exists(_directoryPath))
            {
                _logger.LogInformation("GameTypeExtendedMetadata directory does not exist yet: {DirectoryPath}", _directoryPath);
                return;
            }

            var jsonFiles = Directory.GetFiles(_directoryPath, "*.json");
            _logger.LogInformation("Found {Count} metadata file(s) in {DirectoryPath}", jsonFiles.Length, _directoryPath);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var metadata = JsonSerializer.Deserialize<GameTypeExtendedMetadata>(fs);
                        
                        if (metadata != null && !string.IsNullOrEmpty(metadata.GameTypeKey))
                        {
                            _metadata[metadata.GameTypeKey] = metadata;
                            _logger.LogDebug("Loaded metadata for game type: {GameType} from {FilePath}", metadata.GameTypeKey, filePath);
                        }
                        else
                        {
                            _logger.LogWarning("Invalid metadata in file {FilePath}: Missing GameTypeKey", filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading metadata from file {FilePath}", filePath);
                }
            }
        }

        /// <summary>
        /// Saves a game type's metadata to its individual file
        /// </summary>
        private async Task SaveData(string gameTypeKey)
        {
            if (!_metadata.TryGetValue(gameTypeKey, out var metadata))
            {
                _logger.LogWarning("Attempted to save non-existent game type: {GameType}", gameTypeKey);
                return;
            }

            await _saveLock.WaitAsync();
            try
            {
                var filePath = GetFilePathForGameType(gameTypeKey);
                _logger.LogDebug("Saving metadata for {GameType} to {FilePath}", gameTypeKey, filePath);

                // Ensure the directory exists
                if (!Directory.Exists(_directoryPath))
                {
                    Directory.CreateDirectory(_directoryPath);
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await JsonSerializer.SerializeAsync(fs, metadata, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await fs.FlushAsync();
                }
                
                _logger.LogInformation("Metadata for {GameType} saved successfully to {FilePath}", gameTypeKey, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving metadata for game type {GameType}", gameTypeKey);
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Deletes a game type's metadata file
        /// </summary>
        private async Task DeleteDataFile(string gameTypeKey)
        {
            await _saveLock.WaitAsync();
            try
            {
                var filePath = GetFilePathForGameType(gameTypeKey);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted metadata file for {GameType}: {FilePath}", gameTypeKey, filePath);
                }
                else
                {
                    _logger.LogDebug("No file to delete for {GameType} at {FilePath}", gameTypeKey, filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting metadata file for game type {GameType}", gameTypeKey);
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        public async Task AddOrUpdate(GameTypeExtendedMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.GameTypeKey))
                throw new ArgumentException("GameTypeKey cannot be null or empty.", nameof(metadata));

            var isNew = !_metadata.ContainsKey(metadata.GameTypeKey);
            _metadata[metadata.GameTypeKey] = metadata;
            
            await SaveData(metadata.GameTypeKey);
            _logger.LogInformation("{Action} metadata for game type: {GameType}", 
                isNew ? "Created" : "Updated", metadata.GameTypeKey);
        }

        public async Task Delete(string gameTypeKey)
        {
            if (_metadata.Remove(gameTypeKey))
            {
                await DeleteDataFile(gameTypeKey);
                _logger.LogInformation("Deleted metadata for game type: {GameType}", gameTypeKey);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent game type: {GameType}", gameTypeKey);
            }
        }

        public Task<GameTypeExtendedMetadata?> Get(string gameTypeKey)
        {
            _metadata.TryGetValue(gameTypeKey, out var metadata);
            return Task.FromResult(metadata);
        }

        public Task<List<GameTypeExtendedMetadata>> GetAll()
        {
            return Task.FromResult(_metadata.Values.ToList());
        }
    }
}
