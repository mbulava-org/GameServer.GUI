using Docker.DotNet;
using Docker.DotNet.Models;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Service for managing game server lifecycle (start, stop, restart).
    /// NOTE: This service is DEPRECATED and should be refactored to use IServiceOperations.
    /// Currently only works in Direct mode where IDockerClient is available.
    /// </summary>
    public class ServerLifecycleService
    {
        private readonly IDockerClient? _client;

        public ServerLifecycleService(IDockerClient? client = null)
        {
            _client = client;
        }

        public async Task StartAsync(string serviceName)
        {
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "ServerLifecycleService requires IDockerClient which is not available in Agent mode. " +
                    "This service needs to be refactored to use IServiceOperations.");
            }

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
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "ServerLifecycleService requires IDockerClient which is not available in Agent mode. " +
                    "This service needs to be refactored to use IServiceOperations.");
            }

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
