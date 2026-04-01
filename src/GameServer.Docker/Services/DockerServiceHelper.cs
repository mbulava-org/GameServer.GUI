using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Repositories;
using GameServer.Docker.Constants;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace GameServer.Docker.Services
{
    [Obsolete("Legacy pre-V2 Docker service helper. Migrate callers to GameServer.Docker.Models.V2 and V2 orchestration services before removing the old repository chain.")]
    public class DockerServiceHelper(ILogger<DockerServiceHelper> logger,
        IServiceOperations serviceOperations,
        IServiceProvider serviceProvider,
        IOptions<Configurations.VolumeDriverConfigOptions> volOptions,
        IOptions<Configurations.NetworkOptions> netOptions,
        INodeAgentDiscovery agentDiscovery,
        WebHostResolver webHostResolver)
    {
        /// <summary>
        /// Helper method to get IGameTypeRepository with proper scoping
        /// </summary>
        private async Task<T> WithRepositoryAsync<T>(Func<IGameTypeRepository, Task<T>> action)
        {
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGameTypeRepository>();
            return await action(repository);
        }

        /// <summary>
        /// Helper method to get IGameTypeRepository with proper scoping (void version)
        /// </summary>
        private async Task WithRepositoryAsync(Func<IGameTypeRepository, Task> action)
        {
            using var scope = serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGameTypeRepository>();
            await action(repository);
        }
        /// <summary>
        /// Builds a Docker Swarm ServiceSpec from a GameServer and GameTypeDefinition.
        /// When updating, preserves settings that weren't explicitly changed.
        /// Volume mounts are IMMUTABLE - they are never changed after initial creation.
        /// </summary>
        private async Task<ServiceSpec> BuildGameServerServiceSpec(
            Models.GameServer server, 
            Models.GameTypeDefinition definition, 
            ServiceSpec? existingSpec = null,
            bool stopService = false)
        {
            var isUpdate = existingSpec != null;
            logger.LogDebug($"Building ServiceSpec for {server.Name} (mode={(isUpdate ? "UPDATE" : "CREATE")})");

            // 1. Build environment variables by merging defaults with server-specific settings
            var env = BuildEnvironmentVariables(server, definition);

            // 2. Build port mappings
            var portConfigs = BuildPortConfigs(server, definition, existingSpec);

            // 3. Build or preserve volume mounts
            // IMPORTANT: Volume mounts are IMMUTABLE - only set on creation, never updated
            IList<Mount> mounts;
            if (isUpdate)
            {
                // Preserve existing mounts - NEVER change volumes during updates
                mounts = existingSpec!.TaskTemplate?.ContainerSpec?.Mounts ?? new List<Mount>();
                logger.LogInformation($"Preserving {mounts.Count} existing volume mounts (volumes are immutable)");
                
                // Log warning if server tried to change volumes
                if (server.Volumes != null && server.Volumes.Any())
                {
                    logger.LogWarning("Volume changes requested during update are ignored. Volumes are immutable after creation.");
                }
            }
            else
            {
                // Create new mounts only during initial creation
                mounts = BuildMounts(server, definition);
                logger.LogInformation($"Created {mounts.Count} new volume mounts");
            }

            // 4. Extract memory reservation from settings (if specified)
            long? memoryLimit = null;// ExtractMemoryLimit(server, definition); //I'll do something else with this...  
            //TODO:Reservation and Limits need some thought...  

            // 5. Build service name - preserve existing name if updating
            var serviceName = existingSpec?.Name ?? 
                              (string.IsNullOrWhiteSpace(server.ServiceName)
                                  ? $"{server.GameType}_{server.ServerId}"
                                  : server.ServiceName);

            //5a. Fetch extended metadata for this game type
            var extendedMetadata = await WithRepositoryAsync(repo => repo.GetExtendedMetadataAsync(definition.Key));
            //repository..GetExtendedMetadataAsync(definition.Key);

            // 6. Build labels to identify this as a managed GameServer
            var labels = new Dictionary<string, string>
            {
                [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
                [ServiceLabels.ServerId] = server.ServerId,
                [ServiceLabels.Name] = server.Name,
                [ServiceLabels.Description] = server.Description,
                [ServiceLabels.GameType] = server.GameType
            };

            // 6a. Auto-generate reverse proxy labels if web hosts are defined
            if (extendedMetadata?.WebHosts?.Any() == true)
            {
                var resolvedHosts = webHostResolver.ResolveWebHosts(
                    extendedMetadata.WebHosts,
                    server.Settings);

                if (resolvedHosts.Any())
                {
                    var proxyLabels = GenerateReverseProxyLabels(server, resolvedHosts, serviceName);
                    foreach (var label in proxyLabels)
                    {
                        labels[label.Key] = label.Value;
                    }

                    logger.LogInformation("Generated {Count} reverse proxy labels for {HostCount} web hosts",
                        proxyLabels.Count, resolvedHosts.Count);
                }
            }

            // 7. Preserve existing mode (replicated/global) if updating
            ServiceMode? mode = GenerateServiceMode(stopService, existingSpec?.Mode);

            // 8. Preserve update config if updating, otherwise use defaults - FIXED: Use SwarmUpdateConfig
            SwarmUpdateConfig? updateConfig = existingSpec?.UpdateConfig ?? new SwarmUpdateConfig
            {
                Parallelism = 1,
                FailureAction = "pause",
                Order = "stop-first"
            };

            // 9. Preserve rollback config if exists - FIXED: Use SwarmUpdateConfig
            SwarmUpdateConfig? rollbackConfig = existingSpec?.RollbackConfig;

            // 10. Construct the ServiceSpec
            var serviceSpec = new ServiceSpec
            {
                Name = serviceName,
                Labels = labels,
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = definition.Image,
                        Env = env,
                        Mounts = mounts,
                        Labels = labels,
                        TTY = extendedMetadata?.EnableTTY ?? false
                    },
                    Resources = memoryLimit.HasValue ? new ResourceRequirements
                    {
                        Reservations = new SwarmResources
                        {
                            MemoryBytes = memoryLimit.Value
                        },
                        Limits = new SwarmLimit  // FIXED: Use SwarmLimit not SwarmResources
                        {
                            MemoryBytes = memoryLimit.Value
                        }
                    } : null,
                    RestartPolicy = existingSpec?.TaskTemplate?.RestartPolicy ?? new SwarmRestartPolicy
                    {
                        Condition = "on-failure",
                        MaxAttempts = 3,
                        Delay = 5000000000
                    }
                },
                Networks = CreateNetworkConfig(existingSpec?.Networks, extendedMetadata?.WebHosts).Result,
                EndpointSpec = new EndpointSpec
                {
                    Ports = portConfigs
                },
                Mode = mode,
                UpdateConfig = updateConfig,
                RollbackConfig = rollbackConfig
            };

            logger.LogDebug($"ServiceSpec built successfully for {serviceName}");
            return serviceSpec;
        }

        /// <summary>
        /// Creates network configuration for the service.
        /// Only attaches to networks if functionality is enabled.
        /// </summary>
        private async Task<IList<NetworkAttachmentConfig>> CreateNetworkConfig(
            IList<NetworkAttachmentConfig>? existing,
            List<Models.WebHostDefinition>? webHosts)
        {
            var opts = netOptions.Value;
            var networks = new List<NetworkAttachmentConfig>();

            //We don't need to connect to any network if we don't have any web hosts, since we won't be exposing ports or using reverse proxy features

            // Add load balancer network ONLY if web hosts are configured
            if (webHosts?.Any() == true)
            {
                var lbNetwork = opts?.LoadBalancerNetwork;
                if (!string.IsNullOrWhiteSpace(lbNetwork) && lbNetwork != opts?.NetworkName)
                {
                    logger.LogInformation("Attaching service to load balancer network: {NetworkName} (for {Count} web hosts)", 
                        lbNetwork, webHosts.Count);
                    networks.Add(new NetworkAttachmentConfig
                    {
                        Target = lbNetwork,
                        Aliases = new List<string>(),
                        DriverOpts = null,
                    });
                }
                else if (string.IsNullOrWhiteSpace(lbNetwork))
                {
                    logger.LogWarning("Service has web hosts configured but no LoadBalancerNetwork is set. Web interfaces will not be accessible via reverse proxy.");
                }
            }

            if (!networks.Any())
            {
                logger.LogDebug("Service will be created without network attachments (ports will be exposed directly).");
            }

            return networks;
        }

        private ServiceMode GenerateServiceMode(bool stopped, ServiceMode? existing)
        {
            if (!stopped)
            {
                return new ServiceMode
                    {
                        Replicated = new ReplicatedService
                        {
                            Replicas = 1,

                        }
                    };
            }
            else
            {
                return new ServiceMode
                {
                    Replicated = new ReplicatedService
                    {
                        Replicas = 0,
                    }
                };
            }
        }

        /// <summary>
        /// Builds environment variable list by merging definition defaults with server settings.
        /// Server settings override defaults.
        /// </summary>
        private IList<string> BuildEnvironmentVariables(Models.GameServer server, Models.GameTypeDefinition definition)
        {
            var envDict = new Dictionary<string, string>();

            // Start with definition defaults
            if (definition.DefaultSettings != null)
            {
                foreach (var kvp in definition.DefaultSettings)
                {
                    envDict[kvp.Key] = kvp.Value;
                }
            }

            // Override with server-specific settings
            if (server.Settings != null)
            {
                foreach (var kvp in server.Settings)
                {
                    envDict[kvp.Key] = kvp.Value;
                }
            }

            // Convert to Docker env format: "KEY=VALUE"
            var result = envDict.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
            
            logger.LogDebug($"Built {result.Count} environment variables");
            return result;
        }

        /// <summary>
        /// Builds Docker port configurations from server and definition ports.
        /// Prefers server-specific port mappings if present, otherwise uses definition defaults.
        /// During updates, can preserve existing ports or allow changes based on requirements.
        /// </summary>
        private IList<PortConfig> BuildPortConfigs(
            Models.GameServer server, 
            Models.GameTypeDefinition definition,
            ServiceSpec? existingSpec = null)
        {
            var portConfigs = new List<PortConfig>();

            // If server has explicit port mappings, use those
            if (server.Ports != null && server.Ports.Any())
            {
                foreach (var port in server.Ports)
                {
                    portConfigs.Add(new PortConfig
                    {
                        Protocol = port.Protocol ?? "tcp",
                        TargetPort = port.ContainerPort,
                        PublishedPort = port.PublishedPort,
                        PublishMode = "ingress"
                    });
                }
            }
            else if (existingSpec?.EndpointSpec?.Ports != null && existingSpec.EndpointSpec.Ports.Any())
            {
                // During updates, preserve existing ports if server doesn't specify new ones
                logger.LogDebug("Preserving existing port configurations");
                portConfigs.AddRange(existingSpec.EndpointSpec.Ports);
            }
            else if (definition.Ports != null && definition.Ports.Any())
            {
                // Use definition defaults if server doesn't specify ports
                foreach (var port in definition.Ports)
                {
                    portConfigs.Add(new PortConfig
                    {
                        Protocol = port.Protocol ?? "tcp",
                        TargetPort = port.Port,
                        PublishedPort = port.Port,
                        PublishMode = "ingress"
                    });
                }
            }

            logger.LogDebug($"Built {portConfigs.Count} port configurations");
            return portConfigs;
        }

        /// <summary>
        /// Builds Docker mount configurations from server and definition volumes.
        /// ONLY called during initial service creation - never during updates.
        /// Volume mounts are IMMUTABLE after creation.
        /// </summary>
        private IList<Mount> BuildMounts(Models.GameServer server, Models.GameTypeDefinition definition)
        {
            var mounts = new List<Mount>();
            var volConfigOptions = volOptions?.Value;
            
            // If server has explicit volumes, use those
            if (server.Volumes != null && server.Volumes.Any())
            {
                if (volConfigOptions == null)
                {
                    throw new InvalidOperationException("VolumeDriverConfigOptions must be configured to use volume mounts.");
                }
                
                foreach (var vol in server.Volumes)
                {
                    // FIXED: Use string.IsNullOrWhiteSpace instead of vol.Source.IsWhiteSpace()
                    string namedSource = !string.IsNullOrWhiteSpace(vol.Source) 
                        ? vol.Source 
                        : $"{definition.Key}_{server.ServerId}_{vol.Target.Replace("/", "")}";
                    
                    var mount = new Mount
                    {
                        Type = "volume",
                        Source = namedSource,
                        Target = vol.Target,
                        VolumeOptions = new VolumeOptions
                        {
                            Labels = new Dictionary<string, string>
                            {
                                ["gameserver.docker.managed"] = "true",
                                ["gameserver.docker.Id"] = server.ServerId,
                                ["gameserver.docker.target"] = vol.Target
                            },
                            DriverConfig = new Driver
                            {
                                Name = volConfigOptions.Name,
                                Options = new Dictionary<string, string>()
                            }
                        }
                    };
                    
                    // Build volume driver options
                    mount.VolumeOptions.DriverConfig.Options.Add("type", volConfigOptions.Options.type);
                    mount.VolumeOptions.DriverConfig.Options.Add("device", 
                        volConfigOptions.Options.device
                            .Replace("{SubPathFormat}", volConfigOptions.SubPathFormat)
                            .Replace("{RootStoragePath}", volConfigOptions.RootStoragePath)
                            .Replace("{serverId}", server.ServerId)
                            .Replace("{Source}", vol.Target.Replace("/", ""))
                            .Replace("{gameTypeKey}", definition.Key)
                    );        
                    mount.VolumeOptions.DriverConfig.Options.Add("o", volConfigOptions.Options.o);

                    // Create local storage path
                    string localPath = Path.Join(volConfigOptions.LocalStoragePath,
                        volConfigOptions.SubPathFormat
                            .Replace("{RootStoragePath}", volConfigOptions.RootStoragePath)
                            .Replace("{serverId}", server.ServerId)
                            .Replace("{Source}", vol.Target.Replace("/", ""))
                            .Replace("{gameTypeKey}", definition.Key)
                        );

                    try
                    {
                        logger.LogDebug("Creating volume storage path: {LocalPath}", localPath);
                        Directory.CreateDirectory(localPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to create local storage path: {LocalPath}", localPath);
                    }

                    mounts.Add(mount);
                }
            }
            else if (definition.Volumes != null && definition.Volumes.Any())
            {
                throw new ArgumentException("Server must specify volume mappings when definition defines volumes.");
            }

            logger.LogDebug($"Built {mounts.Count} volume mounts");
            return mounts;
        }


        public async Task<List<Models.GameServer>> ListGameServersAsync()
        {
            logger.LogInformation("Fetching managed services from Docker Swarm...");

            // Filter services by managed label to reduce API load
            var filters = new ServiceFilter
            {
                Label = [$"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}"]
            };

            var services = (await serviceOperations.ListServicesAsync(labelFilter: filters.Label.First())).ToList();
            logger.LogInformation($"Found {services.Count} managed game server services");

            if (services.Count == 0)
            {
                return [];
            }

            // Fetch tasks ONLY for these services in parallel - one call per service
            logger.LogInformation("Fetching tasks for managed services in parallel...");
            var taskFetchTasks = services.Select(async svc =>
            {
                var tasks = await GetTasksForSwarmServiceAsync(svc.ID);
                return new { ServiceId = svc.ID, Tasks = tasks };
            });

            var taskResults = await Task.WhenAll(taskFetchTasks);

            // Build the tasksByService dictionary from filtered results
            var tasksByService = taskResults.ToDictionary(r => r.ServiceId, r => r.Tasks);
            var totalTasks = tasksByService.Values.Sum(t => t.Count);

            logger.LogInformation($"Fetched {totalTasks} tasks across {services.Count} services");

            logger.LogInformation("Converting services to GameServers in parallel...");

            // Process all services in parallel, passing pre-fetched tasks
            var serverTasks = services.Select(svc => TryCastGameServer(svc, tasksByService));
            var serversWithNulls = await Task.WhenAll(serverTasks);

            // Filter out non-GameServer services (nulls) - should be none since we pre-filtered
            var servers = serversWithNulls.Where(s => s != null).Select(s => s!).ToList();

            logger.LogInformation($"Converted {servers.Count} GameServers");

            return servers;
        }

        private async Task<Models.GameServer?> TryCastGameServer(SwarmService service, Dictionary<string, List<TaskResponse>>? tasksByService = null)
        {
            // Guard against services without labels
            if (service.Spec?.Labels == null)
            {
                return null;
            }

            if (!service.Spec.Labels.ContainsKey(ServiceLabels.Managed) || service.Spec.Labels[ServiceLabels.Managed] != ServiceLabels.ManagedValue)
            {
                return null;
            }
            //Otherwise, we have a managed GameServer
            var item = new Models.GameServer();
            if (service.Spec.Labels.ContainsKey(ServiceLabels.ServerId))
            {
                item.ServerId = service.Spec.Labels[ServiceLabels.ServerId];
            }
            if (service.Spec.Labels.ContainsKey(ServiceLabels.Name))
            {
                item.Name = service.Spec.Labels[ServiceLabels.Name];
            }
            if (service.Spec.Labels.ContainsKey(ServiceLabels.Description))
            {
                item.Description = service.Spec.Labels[ServiceLabels.Description];
            }
            if (service.Spec.Labels.ContainsKey(ServiceLabels.GameType))
            {
                item.GameType = service.Spec.Labels[ServiceLabels.GameType];
            }

            item.ServiceName = service.Spec.Name;


            if (service.Spec.TaskTemplate != null && service.Spec.TaskTemplate.ContainerSpec != null
                && service.Spec.TaskTemplate.ContainerSpec.Env != null)
            {
                foreach (var env in service.Spec.TaskTemplate.ContainerSpec.Env)
                {
                    var kvp = env.Split('=', 2);
                    if (kvp.Length == 2)
                    {
                        item.Settings[kvp[0]] = kvp[1];
                    }
                }
            }

            if (service.Spec.TaskTemplate != null && service.Spec.TaskTemplate.ContainerSpec != null
                && service.Spec.TaskTemplate.ContainerSpec.Mounts != null)
            {
                foreach (var vol in service.Spec.TaskTemplate.ContainerSpec.Mounts)
                {
                    var volumeDef = new Models.VolumeDefinition
                    {
                        Source = vol.Source,
                        Target = vol.Target
                    };
                    item.Volumes.Add(volumeDef);
                }
            }

            if (service.Endpoint?.Ports != null)
            {
                foreach (var port in service.Endpoint.Ports)
                {
                    var portDef = new Models.PortMapping
                    {
                        PublishedPort = port.PublishedPort,
                        ContainerPort = port.TargetPort,
                        Protocol = port.Protocol
                    };
                    item.Ports.Add(portDef);
                }
            }

            //Get Status...
            // Use pre-fetched tasks if available, otherwise fetch them
            List<TaskResponse> tasks;
            if (tasksByService != null && tasksByService.TryGetValue(service.ID, out var cachedTasks))
            {
                tasks = cachedTasks;
            }
            else
            {
                // Fallback: fetch tasks for this specific service (backwards compatibility)
                tasks = await GetTasksForSwarmServiceAsync(service.ID);
            }

            // Get desired replicas from service spec
            var desiredReplicas = (int)(service.Spec?.Mode?.Replicated?.Replicas ?? 0);

            // Count running tasks
            var runningTasks = tasks.Count(t => t.Status?.State == TaskState.Running);

            // Get the container ID from the most recent running task
            var activeStates = new[] { TaskState.Running, TaskState.Starting, TaskState.Preparing };
            var activeTask = tasks
                .Where(t => activeStates.Contains(t.Status?.State ?? TaskState.Shutdown))
                .OrderByDescending(t => t.UpdatedAt)
                .FirstOrDefault();

            item.ContainerId = activeTask?.Status?.ContainerStatus?.ContainerID;

            // Determine status using same logic as ResourceUsage.ServiceStatus
            item.Status = (desiredReplicas, runningTasks) switch
            {
                (0, _) => "Stopped",
                var (d, r) when r == d => "Running",
                var (d, r) when r < d => "Starting",
                var (d, r) when r > d => "Scaling Down",
                _ => "Unknown"
            };

            // Determine if running based on status
            item.IsRunning = item.Status == "Running";

            return item;
        }

        public async Task<Models.GameServer?> GetGameServerById(string Id)
        {
            logger.LogInformation($"Fetching service with ID: {Id}");

            // Use Docker label filter to fetch only the specific service - MUCH faster!
            var filters = new ServiceFilter
            {
                Label = new[] { $"{ServiceLabels.ServerId}={Id}" }
            };

            var services = (await serviceOperations.ListServicesAsync(labelFilter: filters.Label.First())).ToList();

            if (services.Count == 0)
            {
                logger.LogWarning($"No service found with ID: {Id}");
                return null;
            }

            if (services.Count > 1)
            {
                logger.LogWarning($"Multiple services found with ID: {Id}, returning first");
            }

            var service = services.First();

            // Fetch tasks ONLY for this specific service using the Docker service ID
            var serviceTasks = await GetTasksForSwarmServiceAsync(service.ID);

            // Group tasks by service ID for efficient lookup (maintains compatibility with TryCastGameServer)
            var tasksByService = new Dictionary<string, List<TaskResponse>>
            {
                [service.ID] = serviceTasks
            };

            // Convert to GameServer using optimized method
            var gameServer = await TryCastGameServer(service, tasksByService);

            return gameServer;
        }

        public async Task CreateOrUpdateGameServerAsync(Models.GameServer server, Models.GameTypeDefinition definition, bool performShutdown = false)
        {
            var existing = await GetGameServerById(server.ServerId);
            if (existing == null)
            {
                logger.LogInformation($"Creating new GameServer: {server.Name} ({server.ServerId})");
                var serviceSpec = await BuildGameServerServiceSpec(server, definition);

                await serviceOperations.CreateServiceAsync(new ServiceCreateParameters
                {
                    Service = serviceSpec
                });
                logger.LogInformation("GameServer created successfully.");
            }
            else
            {
                logger.LogInformation($"Updating existing GameServer: {server.Name} ({server.ServerId})");

                // Get the existing service from Docker
                var serviceFilter = new ServiceFilter
                {
                    Label = new[] { $"{ServiceLabels.ServerId}={server.ServerId}" }
                };

                var services = await serviceOperations.ListServicesAsync(labelFilter: serviceFilter.Label.First());

                if (!services.Any())
                {
                    logger.LogError("Failed to find existing service for update.");
                    throw new InvalidOperationException($"Existing service '{existing.ServiceName}' not found for update.");
                }
                if (services.Count > 1)
                {
                    logger.LogError("❌ CRITICAL: Multiple services found with ServerId={ServerId}! Services: {ServiceNames}",
                        server.ServerId,
                        string.Join(", ", services.Select(s => $"{s.Spec?.Name}({s.ID})")));
                    throw new InvalidOperationException($"Multiple services found with ServerId '{server.ServerId}'. This indicates duplicate services in Docker Swarm!");
                }

                var service = services.First();
                logger.LogInformation("Found service to update: ID={ServiceId}, Name={ServiceName}", service.ID, service.Spec?.Name);

                // Build updated spec from the new configuration, passing existing spec for reference
                var updatedSpec = await BuildGameServerServiceSpec(server, definition, service.Spec, performShutdown);

                // Update the service with correct version
                logger.LogDebug($"Updating service {service.ID} with version {service.Version.Index}");

                await serviceOperations.UpdateServiceAsync(
                    service.ID,
                    new ServiceUpdateParameters
                    {
                        Service = updatedSpec,
                        Version = (long)service.Version.Index,
                        // Optional: Add update configuration
                        RegistryAuthFrom = "spec"
                    });

                logger.LogInformation("GameServer updated successfully.");
            }
        }

        public async Task<List<string>> GetGameServerServiceLogsAsync(string serverId, int tailLines = 1000)
        {
            logger.LogInformation($"Fetching last {tailLines} lines of service logs for server {serverId}");

            var serviceId = await GetGameServerServiceIdAsync(serverId);
            if (string.IsNullOrEmpty(serviceId))
                throw new InvalidOperationException($"No service found for server {serverId}");

            try
            {
                // Use Node Agent Discovery to fetch service logs from manager node
                var logs = await agentDiscovery.GetServiceLogsAsync(serviceId, tailLines);

                if (logs == null || !logs.Any())
                {
                    logger.LogWarning("No service logs available for server {ServerId}", serverId);
                    return new List<string>();
                }

                logger.LogInformation("Successfully fetched {Count} service log lines for server {ServerId}", logs.Count, serverId);
                return logs;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error fetching service logs for server {serverId}");
                throw;
            }
        }

        
        public async Task<string> GetGameServerServiceIdAsync(string serverId)
        {
            logger.LogInformation($"Getting service ID for server {serverId}");

            var services = await serviceOperations.ListServicesAsync();

            foreach (var svc in services)
            {
                if (svc.Spec.Labels.ContainsKey("gameserver.docker.managed") &&
                    svc.Spec.Labels["gameserver.docker.managed"] == "true" &&
                    svc.Spec.Labels.ContainsKey("gameserver.docker.Id") &&
                    svc.Spec.Labels["gameserver.docker.Id"] == serverId)
                {
                    logger.LogDebug($"Found service {svc.ID} for server {serverId}");
                    return svc.ID;
                }
            }

            logger.LogWarning($"No service found for server {serverId}");
            return string.Empty;
        }


        public async Task<SwarmService?> GetSwarmServiceByServiceId(string serviceId)
        {
            return await serviceOperations.InspectServiceAsync(serviceId);
        }

        public async Task<List<TaskResponse>> GetTasksForSwarmServiceAsync(string serviceId)
        {
            // Query tasks from Swarm Manager only
            var filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["service"] = new Dictionary<string, bool> { [serviceId] = true }
            };

            var allTasks = await serviceOperations.ListTasksAsync(new TasksListParameters { Filters = filters });

            return allTasks.ToList();
        }

        public async Task<string> GetRunningContainerIdForGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            if (server == null)
                throw new InvalidOperationException($"Server {serverId} not found");

            var serviceDetails = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName);

            var service = serviceDetails.FirstOrDefault();
            if (service == null)
                throw new InvalidOperationException($"Unable to locate Service by name {server.ServiceName}");

            // Find running task/container for this service
            var tasks = await serviceOperations.ListTasksAsync();

            // Accept tasks that are Running, Starting, or Preparing (container might not be fully running yet)
            var activeStates = new[] { TaskState.Running, TaskState.Starting, TaskState.Preparing };
            
            var runningTask = tasks
                .Where(t => t.ServiceID == service.ID && activeStates.Contains(t.Status?.State ?? TaskState.Shutdown))
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            if (runningTask == null)
            {
                logger.LogWarning("No active task found for service {ServiceId} ({ServiceName}). Available tasks: {TaskCount}",
                    service.ID, server.ServiceName, tasks.Count(t => t.ServiceID == service.ID));
            }

            return runningTask?.Status?.ContainerStatus?.ContainerID ?? "";
        }

        internal async Task StartGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            var definition = await WithRepositoryAsync(repo => repo.GetByKeyAsync(server!.GameType));
            if (definition == null)
            {
                throw new ArgumentException($"Unable to locate gameType {server.GameType}");
            }
            await CreateOrUpdateGameServerAsync(server, definition);
        }

        internal async Task StopGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            var definition = await WithRepositoryAsync(repo => repo.GetByKeyAsync(server!.GameType));
            if (definition == null)
            {
                throw new ArgumentException($"Unable to locate gameType {server.GameType}");
            }
            await CreateOrUpdateGameServerAsync(server, definition, true);
        }

        internal async Task DeleteGameServerAsync(string serverId, bool removeStorage = false)
        {
            logger.LogInformation("Deleting Docker service for server {ServerId}", serverId);

            var server = await GetGameServerById(serverId);
            if (server == null)
            {
                logger.LogWarning("Server {ServerId} not found", serverId);
                return;
            }

            // Remove the Docker service
            if (!string.IsNullOrEmpty(server.ServiceName))
            {
                try
                {
                    logger.LogInformation("Removing Docker service {ServiceName}", server.ServiceName);
                    await serviceOperations.RemoveServiceAsync(server.ServiceName);
                    logger.LogInformation("Docker service {ServiceName} removed successfully", server.ServiceName);
                }
                catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogWarning("Docker service {ServiceName} not found (may already be deleted)", server.ServiceName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to remove Docker service {ServiceName}", server.ServiceName);
                    throw;
                }
            }

            foreach (var v in server.Volumes)
            {
                var subFolder = volOptions?.Value?.SubPathFormat
                    .Replace("{serverId}", server.ServerId)
                    .Replace("{Source}", v.Target.Replace("/", ""))
                    .Replace("{gameTypeKey}", server.GameType);
                
                // Only process if we have valid paths
                if (!string.IsNullOrEmpty(volOptions?.Value?.LocalStoragePath) && !string.IsNullOrEmpty(subFolder))
                {
                    var mappedPath = Path.Combine(volOptions.Value.LocalStoragePath, subFolder);
                    if (removeStorage)
                    {
                        logger.LogInformation("Deleting storage for volume {Volume} at path {Path}", v.Target, mappedPath);
                        try
                        {
                            if (Directory.Exists(mappedPath))
                            {
                                Directory.Delete(mappedPath, recursive: true);
                                logger.LogInformation("Storage for volume {Volume} deleted successfully", v.Target);
                        }
                        else
                        {
                            logger.LogWarning("Storage path {Path} for volume {Volume} does not exist", mappedPath, v.Target);
                        }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to delete storage for volume {Volume} at path {Path}", v.Target, mappedPath);
                            // Continue with other deletions even if one fails
                        }
                    }
                    else
                    {
                        logger.LogInformation("Preserving storage for volume {Volume} at path {Path}", v.Target, mappedPath);
                    }
                }
                else
                {
                    logger.LogWarning("Skipping volume cleanup - invalid path configuration for volume {Volume}", v.Target);
                }

            }
           
        }

        /// <summary>
        /// Query containers by Docker label to find a specific container
        /// </summary>
        public async Task<string?> GetContainerIdByLabelAsync(string labelKey, string labelValue)
        {
            try
            {
                logger.LogDebug("Querying containers with label {LabelKey}={LabelValue}", labelKey, labelValue);

                // Build filter to query containers by label
                var filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool>
                    {
                        [$"{labelKey}={labelValue}"] = true
                    }
                };

                // TODO: Container operations should go through agents
                // This volume cleanup logic needs to be refactored to use agent APIs
                logger.LogWarning("Volume cleanup is disabled when using Agent mode. Manual cleanup may be required.");
                /*
                var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = false, // Only running containers
                    Filters = filters
                });

                if (!containers.Any())
                {
                    logger.LogWarning("No running container found with label {LabelKey}={LabelValue}", labelKey, labelValue);
                    return null;
                }

                if (containers.Count > 1)
                {
                    logger.LogWarning("Multiple containers found with label {LabelKey}={LabelValue}, using first one", 
                        labelKey, labelValue);
                }

                var container = containers.First();
                logger.LogInformation("Found container {ContainerId} ({Names}) with label {LabelKey}={LabelValue}", 
                    container.ID, string.Join(", ", container.Names), labelKey, labelValue);

                return container.ID;
                */
                return null; // Disabled in Agent mode
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to query container by label {LabelKey}={LabelValue}", labelKey, labelValue);
                return null;
            }
        }

        /// <summary>
        /// Generates reverse proxy labels based on the configured provider.
        /// </summary>
        private Dictionary<string, string> GenerateReverseProxyLabels(
            Models.GameServer server,
            List<ResolvedWebHost> webHosts,
            string serviceName)
        {
            var provider = netOptions.Value?.LoadBalancerProvider?.ToLowerInvariant() ?? "none";

            return provider switch
            {
                "traefik" => GenerateTraefikLabels(server, webHosts, serviceName),
                //"nginx" => GenerateNginxLabels(server, webHosts, serviceName),
                //"caddy" => GenerateCaddyLabels(server, webHosts, serviceName),
                "none" => new Dictionary<string, string>(),
                _ => throw new NotSupportedException($"Load balancer provider '{provider}' is not supported. Supported providers: traefik & none")
            };
        }

        /// <summary>
        /// Generates Traefik-specific labels for service discovery and routing.
        /// </summary>
        private Dictionary<string, string> GenerateTraefikLabels(
            Models.GameServer server,
            List<ResolvedWebHost> webHosts,
            string serviceName)
        {
            var labels = new Dictionary<string, string>();

            // Global enable for Traefik
            labels["traefik.enable"] = "true";

            // Generate labels for each web host
            for (int i = 0; i < webHosts.Count; i++)
            {
                var host = webHosts[i];

                // Create unique router name (first host uses base name, others append path segment)
                var routerName = i == 0
                    ? serviceName
                    : $"{serviceName}-{host.PathSegment}";

                // Build path prefix (first host gets base path, others get subpaths)
                var pathPrefix = i == 0
                    ? $"/game-{server.ServerId}"
                    : $"/game-{server.ServerId}/{host.PathSegment}";

                // Router rule - match path prefix
                labels[$"traefik.http.routers.{routerName}.rule"] = $"PathPrefix(`{pathPrefix}`)";

                // Link router to service
                labels[$"traefik.http.routers.{routerName}.service"] = routerName;

                // Service backend configuration
                labels[$"traefik.http.services.{routerName}.loadbalancer.server.port"] = host.ContainerPort.ToString();

                // Create strip prefix middleware
                labels[$"traefik.http.middlewares.{routerName}-strip.stripprefix.prefixes"] = pathPrefix;

                // Apply middleware to router
                var middlewares = new List<string> { $"{routerName}-strip" };

                // Add auth middleware if required
                if (host.RequiresAuth)
                {
                    middlewares.Add($"{routerName}-auth");
                    // Note: Auth configuration is typically handled globally in Traefik config
                }

                labels[$"traefik.http.routers.{routerName}.middlewares"] = string.Join(",", middlewares);

                logger.LogDebug("Generated Traefik labels for web host '{Name}' on port {Port} with path {Path}",
                    host.Name, host.ContainerPort, pathPrefix);
            }

            return labels;
        }

        
    }
}


