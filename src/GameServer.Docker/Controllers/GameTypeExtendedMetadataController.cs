using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/gametypes/extended")]
    [Obsolete("Legacy pre-V2 extended metadata API. Migrate to V2 revision metadata before removing the old repository chain.")]
    public class GameTypeExtendedMetadataController : ControllerBase
    {
        private readonly IGameTypeRepository _repository;
        private readonly ILogger<GameTypeExtendedMetadataController> _logger;

        public GameTypeExtendedMetadataController(
            IGameTypeRepository repository,
            ILogger<GameTypeExtendedMetadataController> logger)
        {
            _repository = repository;
            _logger = logger;
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
            var metadata = await _repository.GetExtendedMetadataAsync(gameTypeKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Extended metadata for game type '{gameTypeKey}' not found." });
            }
            return Ok(metadata);
        }

        /// <summary>
        /// Adds or updates extended metadata for a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="metadata">The metadata to add or update</param>
        [HttpPost("{gameTypeKey}")]
        [ProducesResponseType(200, Type = typeof(GameTypeExtendedMetadata))]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Save(string gameTypeKey, [FromBody] GameTypeExtendedMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.GameTypeKey))
            {
                metadata.GameTypeKey = gameTypeKey;
            }
            
            if (gameTypeKey != metadata.GameTypeKey)
            {
                return BadRequest(new { message = "GameTypeKey mismatch." });
            }

            try
            {
                var saved = await _repository.SaveExtendedMetadataAsync(gameTypeKey, metadata);
                _logger.LogInformation("Extended metadata for game type '{GameTypeKey}' saved successfully.", gameTypeKey);
                return Ok(saved);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"GameType '{gameTypeKey}' not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving extended metadata for game type '{GameTypeKey}'", gameTypeKey);
                return StatusCode(500, new { message = "An error occurred while saving extended metadata." });
            }
        }

        /// <summary>
        /// Deletes extended metadata for a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        [HttpDelete("{gameTypeKey}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(string gameTypeKey)
        {
            await _repository.DeleteExtendedMetadataAsync(gameTypeKey);
            _logger.LogInformation("Extended metadata for game type '{GameTypeKey}' deleted.", gameTypeKey);
            return NoContent();
        }

        /// <summary>
        /// Gets metadata for a specific setting
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        [HttpGet("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(200, Type = typeof(SettingMetadata))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetSettingMetadata(string gameTypeKey, string settingKey)
        {
            var metadata = await _repository.GetSettingMetadataAsync(gameTypeKey, settingKey);
            if (metadata == null)
            {
                return NotFound(new { message = $"Setting metadata for '{settingKey}' not found." });
            }
            return Ok(metadata);
        }

        /// <summary>
        /// Gets all setting metadata for a game type
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        [HttpGet("{gameTypeKey}/settings")]
        [ProducesResponseType(200, Type = typeof(Dictionary<string, SettingMetadata>))]
        public async Task<ActionResult<Dictionary<string, SettingMetadata>>> GetAllSettingMetadata(string gameTypeKey)
        {
            var metadata = await _repository.GetAllSettingMetadataAsync(gameTypeKey);
            return Ok(metadata);
        }

        /// <summary>
        /// Updates metadata for a specific setting
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        /// <param name="metadata">The metadata to update</param>
        [HttpPut("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateSettingMetadata(
            string gameTypeKey, 
            string settingKey, 
            [FromBody] SettingMetadata metadata)
        {
            try
            {
                await _repository.UpdateSettingMetadataAsync(gameTypeKey, settingKey, metadata);
                _logger.LogInformation("Setting metadata for '{GameType}.{Setting}' updated.", gameTypeKey, settingKey);
                return Ok(new { message = "Setting metadata updated successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Deletes metadata for a specific setting
        /// </summary>
        /// <param name="gameTypeKey">The game type key</param>
        /// <param name="settingKey">The setting key</param>
        [HttpDelete("{gameTypeKey}/settings/{settingKey}")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> DeleteSettingMetadata(string gameTypeKey, string settingKey)
        {
            await _repository.DeleteSettingMetadataAsync(gameTypeKey, settingKey);
            _logger.LogInformation("Setting metadata for '{GameType}.{Setting}' deleted.", gameTypeKey, settingKey);
            return NoContent();
        }
    }
}

