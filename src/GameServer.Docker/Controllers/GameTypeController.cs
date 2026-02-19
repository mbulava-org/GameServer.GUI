using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories;
using GameServer.Docker.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/gametypes")]
    public class GameTypeController : ControllerBase
    {
        private readonly IGameTypeRepository _repository;
        private readonly ILogger<GameTypeController> _logger;

        public GameTypeController(IGameTypeRepository repository, ILogger<GameTypeController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeDefinition>))]
        public async Task<ActionResult<IEnumerable<GameTypeDefinition>>> GetAll()
        {
            var gameTypes = await _repository.GetAllAsync();
            return Ok(gameTypes);
        }

        [HttpGet("{key}")]
        [ProducesResponseType(200, Type = typeof(GameTypeDefinition))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(string key)
        {
            var gameType = await _repository.GetByKeyAsync(key);
            return gameType is null ? NotFound() : Ok(gameType);
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(GameTypeDefinition))]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Create([FromBody] GameTypeDefinition gameType)
        {
            if (await _repository.ExistsAsync(gameType.Key))
            {
                return Conflict($"GameType with key '{gameType.Key}' already exists");
            }

            var created = await _repository.CreateAsync(gameType);
            return CreatedAtAction(nameof(Get), new { key = created.Key }, created);
        }

        [HttpPut("{key}")]
        [ProducesResponseType(200, Type = typeof(GameTypeDefinition))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Update(string key, [FromBody] GameTypeDefinition gameType)
        {
            if (key != gameType.Key)
            {
                return BadRequest("Key mismatch");
            }

            try
            {
                var updated = await _repository.UpdateAsync(gameType);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{key}")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Delete(string key)
        {
            await _repository.DeleteAsync(key);
            return NoContent();
        }

        [HttpGet("search")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeDefinition>))]
        public async Task<ActionResult<IEnumerable<GameTypeDefinition>>> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query cannot be empty");
            }

            var results = await _repository.SearchAsync(q);
            return Ok(results);
        }

        [HttpGet("with-tty")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeDefinition>))]
        public async Task<ActionResult<IEnumerable<GameTypeDefinition>>> GetWithTTY()
        {
            var results = await _repository.GetWithTTYEnabledAsync();
            return Ok(results);
        }
    }
}
