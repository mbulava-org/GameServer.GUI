
namespace GameServer.API.Controllers
{
    using global::GameServer.API.Services;
    using Microsoft.AspNetCore.Mvc;


    [ApiController]
    [Route("api/ports")]
    public class PortController : ControllerBase
    {
        private readonly PortAllocator _allocator;

        public PortController(PortAllocator allocator)
        {
            _allocator = allocator;
        }

        

        [HttpGet("check/{protocol}/{port}")]
        [ProducesResponseType(200, Type = typeof(bool))]
        public async Task<IActionResult> Check(string protocol, uint port)
        {
            var available = await _allocator.IsProtocolPortAvailable(port, protocol);
            return Ok(available );
        }
    }
}
