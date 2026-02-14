using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Helper service for applying extended metadata to Docker container specifications
    /// </summary>
    public class GameTypeMetadataApplier
    {
        private readonly IGameTypeRepository _repository;
        private readonly ILogger<GameTypeMetadataApplier> _logger;

        public GameTypeMetadataApplier(
            IGameTypeRepository repository,
            ILogger<GameTypeMetadataApplier> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Applies extended metadata to a ContainerSpec
        /// </summary>
        /// <param name="containerSpec">The container specification to modify</param>
        /// <param name="gameTypeKey">The game type key</param>
        /// <returns>The modified ContainerSpec</returns>
        public async Task<ContainerSpec> ApplyMetadata(ContainerSpec containerSpec, string gameTypeKey)
        {
            var metadata = await _repository.GetExtendedMetadataAsync(gameTypeKey);
            if (metadata == null)
            {
                _logger.LogDebug("No extended metadata found for game type '{GameTypeKey}'", gameTypeKey);
                return containerSpec;
            }

            // Apply TTY settings
            if (metadata.EnableTTY)
            {
                containerSpec.TTY = true;
                _logger.LogDebug("Enabled TTY for game type '{GameTypeKey}'", gameTypeKey);
            }

            return containerSpec;
        }

        /// <summary>
        /// Validates server settings against extended metadata rules
        /// </summary>
        /// <param name="server">The game server to validate</param>
        /// <param name="gameTypeKey">The game type key</param>
        /// <returns>List of validation errors, empty if valid</returns>
        public async Task<List<string>> ValidateSettings(GameServer.Docker.Models.GameServer server, string gameTypeKey)
        {
            var errors = new List<string>();
            var metadata = await _repository.GetExtendedMetadataAsync(gameTypeKey);
            
            if (metadata == null)
            {
                _logger.LogDebug("No extended metadata found for game type '{GameTypeKey}', skipping validation", gameTypeKey);
                return errors;
            }

            foreach (var settingMeta in metadata.SettingsMetadata.Values)
            {
                // Check if setting exists in server settings
                var hasValue = server.Settings.TryGetValue(settingMeta.Key, out var value);

                // Validate required settings
                if (settingMeta.IsRequired && !hasValue)
                {
                    errors.Add($"Setting '{settingMeta.Key}' is required but not provided. {settingMeta.Description}");
                    continue;
                }

                // Validate cannot be empty
                if (hasValue && settingMeta.CannotBeEmpty && string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"Setting '{settingMeta.Key}' cannot be empty. {settingMeta.Description}");
                    continue;
                }

                // Validate against pattern if provided
                if (hasValue && !string.IsNullOrEmpty(settingMeta.ValidationPattern))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", settingMeta.ValidationPattern))
                    {
                        var message = !string.IsNullOrEmpty(settingMeta.ValidationMessage)
                            ? settingMeta.ValidationMessage
                            : $"Setting '{settingMeta.Key}' does not match the required pattern.";
                        errors.Add(message);
                    }
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning("Validation failed for game server '{ServerName}' with {ErrorCount} errors", 
                    server.Name, errors.Count);
            }

            return errors;
        }

        /// <summary>
        /// Applies dynamic port updates from settings to the GameTypeDefinition ports.
        /// When a setting controls a port value, this updates the port mapping accordingly.
        /// </summary>
        /// <param name="server">The game server</param>
        /// <param name="definition">The game type definition</param>
        /// <returns>Updated list of port definitions with setting values applied</returns>
        public async Task<List<PortDefinition>> ApplyDynamicPortMappings(
            GameServer.Docker.Models.GameServer server, 
            GameTypeDefinition definition)
        {
            var metadata = await _repository.GetExtendedMetadataAsync(server.GameType);
            if (metadata == null)
            {
                // No metadata, return original ports
                return new List<PortDefinition>(definition.Ports);
            }

            // Start with a copy of the original ports
            var updatedPorts = new List<PortDefinition>();
            foreach (var port in definition.Ports)
            {
                updatedPorts.Add(new PortDefinition(port.Port, port.Protocol, port.IsDefaultPort));
            }

            // Apply setting-controlled port mappings
            foreach (var settingMeta in metadata.SettingsMetadata.Values)
            {
                if (!settingMeta.MapsToContainerPort || !settingMeta.LinkedContainerPort.HasValue)
                    continue;

                // Find if this setting has a value
                if (!server.Settings.TryGetValue(settingMeta.Key, out var portValue))
                    continue;

                // Parse the port value
                if (!uint.TryParse(portValue, out var newPort))
                {
                    _logger.LogWarning("Setting '{SettingKey}' is marked as port mapping but value '{Value}' is not a valid port number",
                        settingMeta.Key, portValue);
                    continue;
                }

                // Find the port definition to update
                var portToUpdate = updatedPorts.FirstOrDefault(p => 
                    p.Port == settingMeta.LinkedContainerPort.Value && 
                    p.Protocol == settingMeta.PortProtocol);

                if (portToUpdate != null)
                {
                    _logger.LogInformation("Updating port mapping from {OldPort}/{Protocol} to {NewPort}/{Protocol} based on setting '{SettingKey}'",
                        portToUpdate.Port, portToUpdate.Protocol, newPort, portToUpdate.Protocol, settingMeta.Key);
                    
                    portToUpdate.Port = newPort;
                }
                else
                {
                    _logger.LogWarning("Could not find port {LinkedPort}/{Protocol} to update from setting '{SettingKey}'",
                        settingMeta.LinkedContainerPort.Value, settingMeta.PortProtocol, settingMeta.Key);
                }
            }

            return updatedPorts;
        }

        /// <summary>
        /// Gets the default/primary port for user connections
        /// </summary>
        /// <param name="ports">List of port definitions</param>
        /// <returns>The default port, or null if none is marked as default</returns>
        public PortDefinition? GetDefaultPort(List<PortDefinition> ports)
        {
            return ports.FirstOrDefault(p => p.IsDefaultPort) ?? ports.FirstOrDefault();
        }

        /// <summary>
        /// Gets settings metadata organized by category for UI rendering
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <returns>Dictionary of category name to list of settings metadata, ordered by DisplayOrder</returns>
        public async Task<Dictionary<string, List<SettingMetadata>>> GetSettingsByCategory(string gameTypeKey)
        {
            var metadata = await _repository.GetExtendedMetadataAsync(gameTypeKey);
            if (metadata == null)
            {
                return new Dictionary<string, List<SettingMetadata>>();
            }

            var categorized = metadata.SettingsMetadata.Values
                .GroupBy(s => s.Category ?? "Undefined")
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(s => s.DisplayOrder).ToList()
                );

            return categorized;
        }

        /// <summary>
        /// Parses a list-type setting value into individual items
        /// </summary>
        /// <param name="settingKey">The setting key</param>
        /// <param name="value">The setting value</param>
        /// <param name="gameTypeKey">The game type key</param>
        /// <returns>List of parsed items</returns>
        public async Task<List<string>> ParseListSetting(string settingKey, string value, string gameTypeKey)
        {
            var metadata = await _repository.GetExtendedMetadataAsync(gameTypeKey);
            if (metadata == null || !metadata.SettingsMetadata.TryGetValue(settingKey, out var settingMeta))
            {
                // Default to comma-separated
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            if (settingMeta.DataType != "list")
            {
                _logger.LogWarning("Setting '{SettingKey}' is not a list type", settingKey);
                return new List<string> { value };
            }

            var delimiter = settingMeta.ListDelimiter ?? ",";
            return value.Split(delimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
}
