using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Implementation of IServiceOperations that delegates to a manager node agent via HTTP.
    /// This allows the Primary Service to perform service operations without connecting to Docker.
    /// </summary>
    public class ServiceOperationsViaAgent : IServiceOperations
    {
        private readonly IAgentRegistry _agentRegistry;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ServiceOperationsViaAgent> _logger;

        public ServiceOperationsViaAgent(
            IAgentRegistry agentRegistry,
            IHttpClientFactory httpClientFactory,
            ILogger<ServiceOperationsViaAgent> logger)
        {
            _agentRegistry = agentRegistry;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ServiceCreateResponse> CreateServiceAsync(
            ServiceCreateParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogInformation(
                "Creating service via agent: {ServiceName} on manager {NodeName}",
                parameters.Service.Name,
                managerAgent.NodeName);

            // Convert ServiceCreateParameters to agent API format
            var request = new
            {
                ServiceName = parameters.Service.Name,
                Image = parameters.Service.TaskTemplate.ContainerSpec.Image,
                Labels = parameters.Service.Labels ?? new Dictionary<string, string>(),
                Env = ParseEnvironmentVariables(parameters.Service.TaskTemplate.ContainerSpec.Env),
                Ports = ConvertPortConfigs(parameters.Service.EndpointSpec?.Ports),
                Mounts = ConvertMounts(parameters.Service.TaskTemplate.ContainerSpec.Mounts),
                Resources = ConvertResources(parameters.Service.TaskTemplate.Resources),
                RestartPolicy = ConvertRestartPolicy(parameters.Service.TaskTemplate.RestartPolicy),
                Placement = ConvertPlacement(parameters.Service.TaskTemplate.Placement),
                Networks = parameters.Service.TaskTemplate.Networks?.Select(n => n.Target).ToList() ?? new List<string>()
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.PostAsJsonAsync("/api/services", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ServiceOperationResponse>(cancellationToken);

            if (result?.Success != true)
            {
                throw new Exception($"Failed to create service: {result?.Message}");
            }

            _logger.LogInformation("Service created successfully: {ServiceId}", result.ServiceId);

            return new ServiceCreateResponse
            {
                ID = result.ServiceId!
            };
        }

        public async Task UpdateServiceAsync(
            string serviceId,
            ServiceUpdateParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogInformation(
                "Updating service via agent: {ServiceId} on manager {NodeName}",
                serviceId,
                managerAgent.NodeName);

            var request = new
            {
                ServiceId = serviceId,
                Image = parameters.Service.TaskTemplate?.ContainerSpec?.Image,
                Labels = parameters.Service.Labels,
                Env = parameters.Service.TaskTemplate?.ContainerSpec?.Env != null
                    ? ParseEnvironmentVariables(parameters.Service.TaskTemplate.ContainerSpec.Env)
                    : null,
                Resources = ConvertResources(parameters.Service.TaskTemplate?.Resources),
                ForceUpdate = parameters.Service.TaskTemplate?.ForceUpdate > 0
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.PutAsJsonAsync($"/api/services/{serviceId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ServiceOperationResponse>(cancellationToken);

            if (result?.Success != true)
            {
                throw new Exception($"Failed to update service: {result?.Message}");
            }

            _logger.LogInformation("Service updated successfully: {ServiceId}", serviceId);
        }

        public async Task RemoveServiceAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogInformation(
                "Deleting service via agent: {ServiceId} on manager {NodeName}",
                serviceId,
                managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.DeleteAsync($"/api/services/{serviceId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ServiceOperationResponse>(cancellationToken);

            if (result?.Success != true)
            {
                throw new Exception($"Failed to delete service: {result?.Message}");
            }

            _logger.LogInformation("Service deleted successfully: {ServiceId}", serviceId);
        }

        public async Task<IList<SwarmService>> ListServicesAsync(
            ServicesListParameters? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Listing services via agent on manager {NodeName}", managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Build query string for label filter
            var queryString = "";
            if (parameters?.Filters?.Label?.Any() == true)
            {
                queryString = $"?labelFilter={Uri.EscapeDataString(parameters.Filters.Label.First())}";
            }

            var response = await httpClient.GetAsync($"/api/services{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Deserialize using JsonElement to preserve type information
            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (jsonDoc == null)
            {
                throw new Exception("Failed to deserialize response from agent");
            }

            // Extract the nested data
            if (!jsonDoc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) 
                    ? msgProp.GetString() 
                    : "Unknown error";
                throw new Exception($"Failed to list services: {message}");
            }

            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataProp) ||
                !dataProp.TryGetProperty("services", out var servicesProp))
            {
                throw new Exception("Response missing 'data.services' property");
            }

            // Deserialize services array directly from the JSON element
            var services = JsonSerializer.Deserialize<List<SwarmService>>(servicesProp.GetRawText()) 
                ?? new List<SwarmService>();

            _logger.LogDebug("Listed {Count} services via agent", services.Count);

            return services;
        }

        public async Task<SwarmService> InspectServiceAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Inspecting service via agent: {ServiceId} on manager {NodeName}", serviceId, managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.GetAsync($"/api/services/{serviceId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Use JsonDocument to preserve type information (avoid double serialization)
            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (jsonDoc == null)
            {
                throw new Exception("Failed to deserialize response from agent");
            }

            if (!jsonDoc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) 
                    ? msgProp.GetString() 
                    : "Unknown error";
                throw new Exception($"Failed to inspect service: {message}");
            }

            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataProp) ||
                !dataProp.TryGetProperty("service", out var serviceProp))
            {
                throw new Exception("Response missing 'data.service' property");
            }

            // Deserialize service directly from the JSON element
            var service = JsonSerializer.Deserialize<SwarmService>(serviceProp.GetRawText());

            if (service == null)
            {
                throw new Exception($"Failed to deserialize service: {serviceId}");
            }

            return service;
        }

        public async Task<IList<TaskResponse>> ListTasksAsync(
            TasksListParameters? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Listing tasks via agent on manager {NodeName}", managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Build query string for service filter
            var queryString = "";
            if (parameters?.Filters?.TryGetValue("service", out var serviceFilter) == true && serviceFilter.Any())
            {
                var serviceId = serviceFilter.First().Key;
                queryString = $"?serviceId={Uri.EscapeDataString(serviceId)}";
            }

            var response = await httpClient.GetAsync($"/api/tasks{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentApiResponse>(cancellationToken);

            if (result?.Success != true || result.Tasks == null)
            {
                throw new Exception($"Failed to list tasks: {result?.Message}");
            }

            return result.Tasks;
        }

        public async Task<IList<NetworkResponse>> ListNetworksAsync(
            NetworksListParameters? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Listing networks via agent on manager {NodeName}", managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Build query string for name filter
            var queryString = "";
            if (parameters?.Filters?.TryGetValue("name", out var nameFilter) == true && nameFilter.Any())
            {
                var name = nameFilter.First().Key;
                queryString = $"?nameFilter={Uri.EscapeDataString(name)}";
            }

            var response = await httpClient.GetAsync($"/api/networks{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentApiResponse>(cancellationToken);

            if (result?.Success != true || result.Networks == null)
            {
                throw new Exception($"Failed to list networks: {result?.Message}");
            }

            return result.Networks;
        }

        public async Task<NetworkResponse> InspectNetworkAsync(string networkId, CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Inspecting network via agent: {NetworkId} on manager {NodeName}", networkId, managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.GetAsync($"/api/networks/{networkId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentApiResponse>(cancellationToken);

            if (result?.Success != true || result.Network == null)
            {
                throw new Exception($"Failed to inspect network: {result?.Message}");
            }

            return result.Network;
        }

        private Models.NodeAgentEndpoint GetManagerAgent()
        {
            var managerAgent = _agentRegistry.GetHealthyManagerAgent();

            if (managerAgent == null)
            {
                var allAgents = _agentRegistry.GetAllAgents();
                var managerAgents = _agentRegistry.GetManagerAgents();

                throw new InvalidOperationException(
                    $"No healthy manager agent available for service operations. " +
                    $"Total agents: {allAgents.Count}, Manager agents: {managerAgents.Count}, " +
                    $"Healthy managers: {managerAgents.Count(a => a.IsHealthy)}");
            }

            return managerAgent;
        }

        #region Conversion Helpers

        private Dictionary<string, string> ParseEnvironmentVariables(IList<string>? env)
        {
            if (env == null) return new Dictionary<string, string>();

            return env
                .Where(e => e.Contains('='))
                .Select(e => e.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : "");
        }

        private List<object>? ConvertPortConfigs(IList<PortConfig>? ports)
        {
            if (ports == null || !ports.Any()) return null;

            return ports.Select(p => new
            {
                TargetPort = p.TargetPort,
                PublishedPort = p.PublishedPort,
                Protocol = p.Protocol ?? "tcp",
                PublishMode = p.PublishMode
            }).Cast<object>().ToList();
        }

        private List<object>? ConvertMounts(IList<Mount>? mounts)
        {
            if (mounts == null || !mounts.Any()) return null;

            return mounts.Select(m => new
            {
                Type = m.Type,
                Source = m.Source,
                Target = m.Target,
                ReadOnly = m.ReadOnly,
                VolumeOptions = m.VolumeOptions != null
                    ? new Dictionary<string, string>
                    {
                        ["driver"] = m.VolumeOptions.DriverConfig?.Name ?? "local"
                    }.Concat(m.VolumeOptions.DriverConfig?.Options ?? new Dictionary<string, string>())
                        .ToDictionary(kv => kv.Key, kv => kv.Value)
                    : null
            }).Cast<object>().ToList();
        }

        private object? ConvertResources(ResourceRequirements? resources)
        {
            if (resources?.Limits == null) return null;

            return new
            {
                MemoryBytes = resources.Limits.MemoryBytes,
                NanoCPUs = resources.Limits.NanoCPUs
            };
        }

        private object? ConvertRestartPolicy(SwarmRestartPolicy? restartPolicy)
        {
            if (restartPolicy == null) return null;

            return new
            {
                Condition = restartPolicy.Condition ?? "on-failure",
                Delay = restartPolicy.Delay,
                MaxAttempts = restartPolicy.MaxAttempts
            };
        }

        private object? ConvertPlacement(Placement? placement)
        {
            if (placement?.Constraints == null || !placement.Constraints.Any()) return null;

            return new
            {
                Constraints = placement.Constraints
            };
        }

        #endregion

        // Helper class for deserializing agent responses
        private class ServiceOperationResponse
        {
            public bool Success { get; set; }
            public string? ServiceId { get; set; }
            public string? Message { get; set; }
            public Dictionary<string, object>? Data { get; set; }
        }

        private class AgentApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public int Count { get; set; }
            public List<TaskResponse>? Tasks { get; set; }
            public List<NetworkResponse>? Networks { get; set; }
            public NetworkResponse? Network { get; set; }
        }
    }
}
