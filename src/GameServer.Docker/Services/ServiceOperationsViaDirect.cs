using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Implementation of IServiceOperations that uses direct Docker client connection.
    /// This is the legacy implementation for backward compatibility.
    /// </summary>
    public class ServiceOperationsViaDirect : IServiceOperations
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<ServiceOperationsViaDirect> _logger;

        public ServiceOperationsViaDirect(
            IDockerClient dockerClient,
            ILogger<ServiceOperationsViaDirect> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        public async Task<ServiceCreateResponse> CreateServiceAsync(
            ServiceCreateParameters parameters,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating service via direct Docker connection: {ServiceName}", parameters.Service.Name);
            return await _dockerClient.Swarm.CreateServiceAsync(parameters, cancellationToken);
        }

        public async Task UpdateServiceAsync(
            string serviceId,
            ServiceUpdateParameters parameters,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Updating service via direct Docker connection: {ServiceId}", serviceId);
            await _dockerClient.Swarm.UpdateServiceAsync(serviceId, parameters, cancellationToken);
        }

        public async Task RemoveServiceAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting service via direct Docker connection: {ServiceId}", serviceId);
            await _dockerClient.Swarm.RemoveServiceAsync(serviceId, cancellationToken);
        }

        public async Task<IList<SwarmService>> ListServicesAsync(
            string? labelFilter = null,
            string? serviceName = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Listing services via direct Docker connection");

            var parameters = new ServiceListParameters();

            if (!string.IsNullOrWhiteSpace(labelFilter) || !string.IsNullOrWhiteSpace(serviceName))
            {
                parameters.Filters = new Dictionary<string, IDictionary<string, bool>>();

                if (!string.IsNullOrWhiteSpace(labelFilter))
                {
                    parameters.Filters["label"] = new Dictionary<string, bool> { [labelFilter] = true };
                }

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    parameters.Filters["name"] = new Dictionary<string, bool> { [serviceName] = true };
                }
            }

            var services = await _dockerClient.Swarm.ListServicesAsync(parameters, cancellationToken);
            return services.ToList();
        }

        public async Task<SwarmService> InspectServiceAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Inspecting service via direct Docker connection: {ServiceId}", serviceId);
            return await _dockerClient.Swarm.InspectServiceAsync(serviceId, cancellationToken);
        }

        public async Task<IList<TaskResponse>> ListTasksAsync(
            TasksListParameters? parameters = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Listing tasks via direct Docker connection");
            var tasks = await _dockerClient.Tasks.ListAsync(parameters ?? new TasksListParameters(), cancellationToken);
            return tasks.ToList();
        }

        public async Task<IList<NetworkResponse>> ListNetworksAsync(
            NetworksListParameters? parameters = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Listing networks via direct Docker connection");
            var networks = await _dockerClient.Networks.ListNetworksAsync(parameters ?? new NetworksListParameters(), cancellationToken);
            return networks.ToList();
        }

        public async Task<NetworkResponse> InspectNetworkAsync(string networkId, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Inspecting network via direct Docker connection: {NetworkId}", networkId);
            return await _dockerClient.Networks.InspectNetworkAsync(networkId, cancellationToken);
        }
    }
}
