using Docker.DotNet;
using Docker.DotNet.Models;

namespace GameServer.Docker.Services
{
    public class ServerLifecycleService
    {
        private readonly IDockerClient _client;

        public ServerLifecycleService(IDockerClient client)
        {
            _client = client;
        }

        public async Task StartAsync(string serviceName)
        {
            await _client.Swarm.UpdateServiceAsync(
                serviceName,
                new ServiceUpdateParameters
                {
                    Service = new ServiceSpec
                    {
                        Mode = new ServiceMode
                        {
                            Replicated = new ReplicatedService
                            {
                                Replicas = 1
                            }
                        }
                    }
                }
            );
        }

        public async Task StopAsync(string serviceName)
        {
            await _client.Swarm.UpdateServiceAsync(
                serviceName,
                new ServiceUpdateParameters
                {
                    Service = new ServiceSpec
                    {
                        Mode = new ServiceMode
                        {
                            Replicated = new ReplicatedService
                            {
                                Replicas = 0
                            }
                        }
                    }
                }
            );
        }

        public async Task RestartAsync(string serviceName)
        {
            await StopAsync(serviceName);
            await Task.Delay(1000);
            await StartAsync(serviceName);
        }
    }

}
