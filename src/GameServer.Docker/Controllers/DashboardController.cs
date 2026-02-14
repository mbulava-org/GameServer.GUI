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
    public class DashboardController : ControllerBase
    {
        private readonly IGameTypeRepository _repository;
        private readonly ServerLifecycleService _lifecycle;
        private readonly IGameServerManager _manager;

        //// In a real system, you'd load servers from a DB or JSON file.
        //private static readonly List<Models.GameServer> _servers = new();

        public DashboardController(
            IGameTypeRepository repository,
            ServerLifecycleService lifecycle,
            IGameServerManager manager)
        {
            _repository = repository;
            _lifecycle = lifecycle;
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
