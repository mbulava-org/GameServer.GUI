using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/gametypes/extended")]
    public class GameTypeExtendedMetadataController : ControllerBase
    {
        private readonly IGameTypeExtendedMetadataRegistry _registry;
        private readonly ILogger<GameTypeExtendedMetadataController> _logger;

        public GameTypeExtendedMetadataController(
            IGameTypeExtendedMetadataRegistry registry,
            ILogger<GameTypeExtendedMetadataController> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        /// <summary>
        /// Gets all extended metadata entries
        /// </summary>
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeExtendedMetadata>))]
        public async Task<ActionResult<IEnumerable<GameTypeExtendedMetadata>>> GetAll()
        {
            var metadata = await _registry.GetAll();
            return Ok(metadata);
        }

        /// <summary>
        /// Gets extended metadata for a specific game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        [HttpGet("{gameTypeKey}")]
        [ProducesResponseType(200, Type = typeof(GameTypeExtendedMetadata))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(string gameTypeKey)
        {
            var metadata = await _registry.Get(gameTypeKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Extended metadata for game type '{gameTypeKey}' not found." });
            }
            return Ok(metadata);
        }

        /// <summary>
        /// Adds or updates extended metadata for a game type
        /// </summary>
        /// <param name="metadata">The metadata to add or update</param>
        [HttpPost]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Save([FromBody] GameTypeExtendedMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.GameTypeKey))
            {
                return BadRequest(new { message = "GameTypeKey is required." });
            }

            try
            {
                await _registry.AddOrUpdate(metadata);
                _logger.LogInformation("Extended metadata for game type '{GameTypeKey}' saved successfully.", metadata.GameTypeKey);
                return Ok(new { message = "Extended metadata saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving extended metadata for game type '{GameTypeKey}'", metadata.GameTypeKey);
                return StatusCode(500, new { message = "An error occurred while saving extended metadata." });
            }
        }

        /// <summary>
        /// Deletes extended metadata for a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        [HttpDelete("{gameTypeKey}")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Delete(string gameTypeKey)
        {
            try
            {
                await _registry.Delete(gameTypeKey);
                _logger.LogInformation("Extended metadata for game type '{GameTypeKey}' deleted successfully.", gameTypeKey);
                return Ok(new { message = "Extended metadata deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting extended metadata for game type '{GameTypeKey}'", gameTypeKey);
                return StatusCode(500, new { message = "An error occurred while deleting extended metadata." });
            }
        }

        /// <summary>
        /// Gets metadata for a specific setting within a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        [HttpGet("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(200, Type = typeof(SettingMetadata))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetSettingMetadata(string gameTypeKey, string settingKey)
        {
            var metadata = await _registry.Get(gameTypeKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Extended metadata for game type '{gameTypeKey}' not found." });
            }

            if (!metadata.SettingsMetadata.TryGetValue(settingKey, out var settingMetadata))
            {
                return NotFound(new { message = $"Setting metadata for '{settingKey}' not found in game type '{gameTypeKey}'." });
            }

            return Ok(settingMetadata);
        }

        /// <summary>
        /// Adds or updates metadata for a specific setting within a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        /// <param name="settingMetadata">The setting metadata to add or update</param>
        [HttpPut("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateSettingMetadata(
            string gameTypeKey,
            string settingKey,
            [FromBody] SettingMetadata settingMetadata)
        {
            var metadata = await _registry.Get(gameTypeKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Extended metadata for game type '{gameTypeKey}' not found." });
            }

            // Ensure the key matches
            settingMetadata.Key = settingKey;
            metadata.SettingsMetadata[settingKey] = settingMetadata;

            await _registry.AddOrUpdate(metadata);
            _logger.LogInformation("Setting metadata for '{SettingKey}' in game type '{GameTypeKey}' updated successfully.", 
                settingKey, gameTypeKey);

            return Ok(new { message = "Setting metadata updated successfully." });
        }

        /// <summary>
        /// Deletes metadata for a specific setting within a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        [HttpDelete("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteSettingMetadata(string gameTypeKey, string settingKey)
        {
            var metadata = await _registry.Get(gameTypeKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Extended metadata for game type '{gameTypeKey}' not found." });
            }

            if (!metadata.SettingsMetadata.Remove(settingKey))
            {
                return NotFound(new { message = $"Setting metadata for '{settingKey}' not found in game type '{gameTypeKey}'." });
            }

            await _registry.AddOrUpdate(metadata);
            _logger.LogInformation("Setting metadata for '{SettingKey}' in game type '{GameTypeKey}' deleted successfully.", 
                settingKey, gameTypeKey);

            return Ok(new { message = "Setting metadata deleted successfully." });
        }
    }
}
