using Docker.DotNet.Models;
using GameServer.API.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace GameServer.API.Services
{
    /// <summary>
    /// Implementation of IServiceOperations that delegates to a manager node agent via HTTP.
    /// This allows the Primary Service to perform service operations without connecting to Docker.
    /// </summary>
    public class ServiceOperationsViaAgent : IServiceOperations
    {
        private readonly IAgentRegistry _agentRegistry;
        private readonly IUdpAgentRegistry _udpAgentRegistry;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ServiceOperationsViaAgent> _logger;

        public ServiceOperationsViaAgent(
            IAgentRegistry agentRegistry,
            IUdpAgentRegistry udpAgentRegistry,
            IHttpClientFactory httpClientFactory,
            ILogger<ServiceOperationsViaAgent> logger)
        {
            _agentRegistry = agentRegistry;
            _udpAgentRegistry = udpAgentRegistry;
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

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [CreateService] Raw JSON from agent: {Json}", rawJson.Length > 300 ? rawJson[..300] : rawJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ServiceOperationResponse>(rawJson, options);

            if (result?.Success != true)
            {
                _logger.LogError("❌ [CreateService] Failed response. JSON: {Json}", rawJson);
                throw new Exception($"Failed to create service: {result?.Message}");
            }

            _logger.LogInformation("✅ [CreateService] Service created: {ServiceId}", result.ServiceId);

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
                Mounts = ConvertMounts(parameters.Service.TaskTemplate?.ContainerSpec?.Mounts),
                Resources = ConvertResources(parameters.Service.TaskTemplate?.Resources),
                ForceUpdate = parameters.Service.TaskTemplate?.ForceUpdate > 0
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var response = await httpClient.PutAsJsonAsync($"/api/services/{serviceId}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [UpdateService] Raw JSON from agent: {Json}", rawJson.Length > 300 ? rawJson[..300] : rawJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ServiceOperationResponse>(rawJson, options);

            if (result?.Success != true)
            {
                _logger.LogError("❌ [UpdateService] Failed response. JSON: {Json}", rawJson);
                throw new Exception($"Failed to update service: {result?.Message}");
            }

            _logger.LogInformation("✅ [UpdateService] Service updated: {ServiceId}", serviceId);
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

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [RemoveService] Raw JSON from agent: {Json}", rawJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ServiceOperationResponse>(rawJson, options);

            if (result?.Success != true)
            {
                _logger.LogError("❌ [RemoveService] Failed response. JSON: {Json}", rawJson);
                throw new Exception($"Failed to delete service: {result?.Message}");
            }

            _logger.LogInformation("✅ [RemoveService] Service deleted: {ServiceId}", serviceId);
        }

        public async Task<IList<SwarmService>> ListServicesAsync(
            string? labelFilter = null,
            string? serviceName = null,
            CancellationToken cancellationToken = default)
        {
            var managerAgent = GetManagerAgent();

            _logger.LogDebug("Listing services via agent on manager {NodeName}", managerAgent.NodeName);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(managerAgent.InternalUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Build query string for label filter
            // Note: ServiceFilter.Label property may throw KeyNotFoundException if not set
            var queryString = string.IsNullOrWhiteSpace(labelFilter)
                ? string.Empty
                : $"?labelFilter={Uri.EscapeDataString(labelFilter)}";

            var response = await httpClient.GetAsync($"/api/services{queryString}", cancellationToken);
            response.EnsureSuccessStatusCode();

            // Read raw JSON for debugging
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 Raw JSON from agent (first 500 chars): {Json}", rawJson.Length > 500 ? rawJson[..500] : rawJson);

            // Deserialize using JsonElement to preserve type information
            var jsonDoc = JsonDocument.Parse(rawJson);

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
                _logger.LogError("❌ Response JSON structure: {Json}", rawJson.Length > 1000 ? rawJson[..1000] : rawJson);
                throw new Exception("Response missing 'data.services' property");
            }

            _logger.LogWarning("📦 Services JSON (first 500 chars): {Json}", 
                servicesProp.GetRawText().Length > 500 ? servicesProp.GetRawText()[..500] : servicesProp.GetRawText());

            // Deserialize services array directly from the JSON element (case-insensitive for camelCase from agent)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var services = JsonSerializer.Deserialize<List<SwarmService>>(servicesProp.GetRawText(), options) 
                ?? new List<SwarmService>();

            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                services = services
                    .Where(service => string.Equals(service.Spec?.Name, serviceName, StringComparison.Ordinal))
                    .ToList();
            }

            _logger.LogDebug("Listed {Count} services via agent", services.Count);

            // Log first service details for debugging
            if (services.Count > 0)
            {
                var first = services[0];
                _logger.LogWarning("🔍 First service: ID={Id}, Spec={HasSpec}, SpecName={Name}", 
                    first.ID, 
                    first.Spec != null, 
                    first.Spec?.Name ?? "NULL");
            }

            return services;
        }

        /// <summary>
        /// Lists services via the manager agent without applying Docker list filters.
        /// </summary>
        public Task<IList<SwarmService>> ListServicesAsync()
        {
            return ListServicesAsync(null, null, CancellationToken.None);
        }

        /// <summary>
        /// Lists services via the manager agent without applying Docker list filters.
        /// </summary>
        public Task<IList<SwarmService>> ListServicesAsync(CancellationToken cancellationToken)
        {
            return ListServicesAsync(null, null, cancellationToken);
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

            // Read raw JSON for debugging
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [InspectService] Raw JSON from agent (first 500 chars): {Json}", rawJson.Length > 500 ? rawJson[..500] : rawJson);

            // Use JsonDocument to preserve type information (avoid double serialization)
            var jsonDoc = JsonDocument.Parse(rawJson);

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
                _logger.LogError("❌ [InspectService] Response missing 'data.service'. JSON: {Json}", rawJson.Length > 1000 ? rawJson[..1000] : rawJson);
                throw new Exception("Response missing 'data.service' property");
            }

            _logger.LogWarning("📦 [InspectService] Service JSON (first 300 chars): {Json}", 
                serviceProp.GetRawText().Length > 300 ? serviceProp.GetRawText()[..300] : serviceProp.GetRawText());

            // Deserialize service directly from the JSON element (case-insensitive for camelCase from agent)
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var service = JsonSerializer.Deserialize<SwarmService>(serviceProp.GetRawText(), options);

            if (service == null)
            {
                _logger.LogError("❌ [InspectService] Failed to deserialize service from JSON");
                throw new Exception($"Failed to deserialize service: {serviceId}");
            }

            _logger.LogWarning("🔍 [InspectService] Result: ID={Id}, Spec={HasSpec}, SpecName={Name}", 
                service.ID, 
                service.Spec != null,
                service.Spec?.Name ?? "NULL");

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

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [ListTasks] Raw JSON from agent (first 300 chars): {Json}", rawJson.Length > 300 ? rawJson[..300] : rawJson);

            // Use camelCase options to match ASP.NET Core default
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<AgentApiResponse>(rawJson, options);

            if (result?.Success != true || result.Tasks == null)
            {
                _logger.LogError("❌ [ListTasks] Failed response. JSON: {Json}", rawJson.Length > 500 ? rawJson[..500] : rawJson);
                throw new Exception($"Failed to list tasks: {result?.Message}");
            }

            _logger.LogWarning("✅ [ListTasks] Found {Count} tasks, First task has Status: {HasStatus}", 
                result.Tasks.Count, 
                result.Tasks.Count > 0 ? result.Tasks[0].Status != null : false);

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

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [ListNetworks] Raw JSON from agent (first 300 chars): {Json}", rawJson.Length > 300 ? rawJson[..300] : rawJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<AgentApiResponse>(rawJson, options);

            if (result?.Success != true || result.Networks == null)
            {
                _logger.LogError("❌ [ListNetworks] Failed response. JSON: {Json}", rawJson.Length > 500 ? rawJson[..500] : rawJson);
                throw new Exception($"Failed to list networks: {result?.Message}");
            }

            _logger.LogWarning("✅ [ListNetworks] Found {Count} networks", result.Networks.Count);

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

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("📥 [InspectNetwork] Raw JSON from agent (first 300 chars): {Json}", rawJson.Length > 300 ? rawJson[..300] : rawJson);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<AgentApiResponse>(rawJson, options);

            if (result?.Success != true || result.Network == null)
            {
                _logger.LogError("❌ [InspectNetwork] Failed response. JSON: {Json}", rawJson.Length > 500 ? rawJson[..500] : rawJson);
                throw new Exception($"Failed to inspect network: {result?.Message}");
            }

            _logger.LogWarning("✅ [InspectNetwork] Network: ID={Id}, Name={Name}", result.Network.ID, result.Network.Name);

            return result.Network;
        }

        private Models.NodeAgentEndpoint GetManagerAgent()
        {
            var managerAgent = _agentRegistry.GetHealthyManagerAgent();

             if (managerAgent != null)
             {
                 return managerAgent;
             }

             managerAgent = _udpAgentRegistry
                 .GetAllAgents()
                 .FirstOrDefault(agent => agent.IsManagerNode && agent.IsHealthy);

            if (managerAgent == null)
            {
                var allAgents = _agentRegistry.GetAllAgents();
                var managerAgents = _agentRegistry.GetManagerAgents();
                 var udpAgents = _udpAgentRegistry.GetAllAgents();
                 var udpManagerAgents = udpAgents.Where(agent => agent.IsManagerNode).ToList();

                throw new InvalidOperationException(
                    $"No healthy manager agent available for service operations. " +
                     $"Registry agents: {allAgents.Count}, Registry manager agents: {managerAgents.Count}, " +
                     $"Healthy registry managers: {managerAgents.Count(a => a.IsHealthy)}, UDP agents: {udpAgents.Count}, " +
                     $"UDP manager agents: {udpManagerAgents.Count}, Healthy UDP managers: {udpManagerAgents.Count(a => a.IsHealthy)}");
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
                DriverName = m.VolumeOptions?.DriverConfig?.Name,
                VolumeOptions = m.VolumeOptions?.DriverConfig?.Options != null
                    ? new Dictionary<string, string>(m.VolumeOptions.DriverConfig.Options)
                    : null,
                m.VolumeOptions?.Labels,
                OwnerUid = (int?)null,
                OwnerGid = (int?)null,
                Permissions = (string?)null
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
