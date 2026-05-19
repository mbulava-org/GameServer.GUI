using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories;
using GameServer.Docker.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks.Dataflow;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Obsolete("Legacy pre-V2 dashboard flow. Migrate to GameServer.Docker.Models.V2 and V2 query services before removing the old repository chain.")]
    public class DashboardController : ControllerBase
    {
        private readonly IGameTypeRepository _repository;
        private readonly IGameServerManager _manager;

        //// In a real system, you'd load servers from a DB or JSON file.
        //private static readonly List<Models.GameServer> _servers = new();

        public DashboardController(
            IGameTypeRepository repository,
            IGameServerManager manager)
        {
            _repository = repository;
            _manager = manager;
        }

        [HttpGet("servers")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<GameServerDashboardItem>))]
        public async Task<IActionResult> GetServers()
        {
            var list = new List<GameServerDashboardItem>();
            var servers = await _manager.ListServersAsync();
            foreach (var server in servers)
            {
                //var running = await _status.IsRunningAsync(server.ServiceName);
                var Ports = string.Join(", ", server.Ports.Select(p => $"{p.PublishedPort}/{p.Protocol}"));
                list.Add(new GameServerDashboardItem
                {
                    ServerId = server.ServerId,
                    Name = server.Name,
                    Description = server.Description,
                    GameType = server.GameType,
                    Ports = Ports,
                    IsRunning = server.IsRunning,
                    ServiceName = server.ServiceName,
                    Status = server.Status
                });
            }
            return Ok(list);
        }

        
    }
}
