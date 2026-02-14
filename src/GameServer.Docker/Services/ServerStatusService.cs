using Docker.DotNet;
using Docker.DotNet.Models;

namespace GameServer.Docker.Services
{
    public class ServerStatusService
    {
        private readonly IDockerClient _client;

        public ServerStatusService(IDockerClient client)
        {
            _client = client;
        }

        public async Task<bool> IsRunningAsync(string serviceName)
        {
            var tasks = await _client.Tasks.ListAsync(new TasksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["service"] = new Dictionary<string, bool> { [serviceName] = true }
                }
            });

            return tasks.Any(t => t.Status.State == TaskState.Running);
        }
    }

}
