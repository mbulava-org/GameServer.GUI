using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/gametypes")]
    public class GameTypeController : ControllerBase
    {
        private readonly IGameTypeRegistry _registry;

        public GameTypeController(IGameTypeRegistry registry)
        {
            _registry = registry;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeDefinition>))]
        public async Task<ActionResult<IEnumerable<GameTypeDefinition>>> GetAll()
        {
            return Ok(await _registry.GetAll());
        }

        [HttpGet("{key}")]
        [ProducesResponseType(200, Type = typeof(GameTypeDefinition))]
        public async Task<IActionResult> Get(string key)
        {
            var def = await _registry.Get(key);
            return def is null ? NotFound() : Ok(def);
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] GameTypeDefinition def)
        {
            await _registry.AddOrUpdate(def);
            return Ok();
        }

        [HttpDelete("{key}")]
        public async Task<IActionResult> Delete(string key)
        {
            await _registry.Delete(key);
            return Ok();
        }
    }

}
