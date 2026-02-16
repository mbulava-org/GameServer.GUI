using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Repositories;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace GameServer.Docker.Services
{
    public class DockerServiceHelper(ILogger<DockerServiceHelper> logger,
        IDockerClient client,
        IGameTypeRepository gameTypeRepository,
        IOptions<Configurations.VolumeDriverConfigOptions> volOptions,
        IOptions<Configurations.NetworkOptions> netOptions)
    {
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
            var memoryLimit = ExtractMemoryLimit(server, definition);

            // 5. Build service name - preserve existing name if updating
            var serviceName = existingSpec?.Name ?? 
                              (string.IsNullOrWhiteSpace(server.ServiceName)
                                  ? $"{server.GameType}_{server.ServerId}"
                                  : server.ServiceName);

            // 6. Build labels to identify this as a managed GameServer
            var labels = new Dictionary<string, string>
            {
                ["gameserver.docker.managed"] = "true",
                ["gameserver.docker.Id"] = server.ServerId,
                ["gameserver.docker.name"] = server.Name,
                ["gameserver.docker.description"] = server.Description,
                ["gameserver.docker.gametype"] = server.GameType
            };

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

            //Fetch extended metadata for this game type to determine if TTY should be enabled
            var extendedMetadata = await gameTypeRepository.GetExtendedMetadataAsync(definition.Key);

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
                Networks = CreateNetworkConfig(existingSpec?.Networks).Result,
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

        private async Task<IList<NetworkAttachmentConfig>> CreateNetworkConfig(IList<NetworkAttachmentConfig>? existing)
        {
            var opts = netOptions.Value;
            
            if (opts != null 
                && !string.IsNullOrEmpty(opts.NetworkName))
            {
                //lookup the network name
                var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool>
                        {
                            [opts.NetworkName] = true
                        }
                    }
                });
                if (networks.Count > 0)
                {
                    logger.LogInformation("Attaching service to network: {NetworkName}", opts.NetworkName);
                    List<NetworkAttachmentConfig> myNets = new List<NetworkAttachmentConfig>
                    {
                        new NetworkAttachmentConfig
                        {
                            Target = opts.NetworkName,
                            Aliases = new List<string>(),
                            DriverOpts = null,
                        }
                    };
                    return myNets;
                }
            }
            logger.LogWarning("No valid network configuration found. Service will be created without network attachments.");
            return new List<NetworkAttachmentConfig>();
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

        /// <summary>
        /// Extracts memory limit from server settings or definition defaults.
        /// </summary>
        private long? ExtractMemoryLimit(Models.GameServer server, Models.GameTypeDefinition definition)
        {
            //Currently these Setting are only in Minecraft and Hytale so lets just check those
            if (definition.Key != "minecraft" && definition.Key != "hytale")
            {
                logger.LogDebug("Memory limit extraction skipped for game type: {GameType}", definition.Key);
                return null;
            }
            // FIXED: Add null checks for dictionaries
            var memoryString = server.Settings?.GetValueOrDefault("MEMORY")
                ?? server.Settings?.GetValueOrDefault("MAX_MEMORY")
                ?? definition.DefaultSettings?.GetValueOrDefault("MEMORY")
                ?? definition.DefaultSettings?.GetValueOrDefault("MAX_MEMORY");

            if (string.IsNullOrWhiteSpace(memoryString))
            {
                logger.LogDebug("No memory limit specified");
                return null;
            }

            

            var parsedMemory = ParseMemoryString(memoryString);
            logger.LogDebug($"Parsed memory limit: {memoryString} -> {parsedMemory} bytes");
            return parsedMemory;
        }

        /// <summary>
        /// Parses memory strings like "1G", "512M", "2048m" to bytes.
        /// </summary>
        private static long? ParseMemoryString(string memory)
        {
            if (string.IsNullOrWhiteSpace(memory))
                return null;

            memory = memory.Trim().ToUpperInvariant();

            long multiplier = 1;
            if (memory.EndsWith("G"))
            {
                multiplier = 1024L * 1024L * 1024L;
                memory = memory[..^1];
            }
            else if (memory.EndsWith("M"))
            {
                multiplier = 1024L * 1024L;
                memory = memory[..^1];
            }
            else if (memory.EndsWith("K"))
            {
                multiplier = 1024L;
                memory = memory[..^1];
            }

            if (long.TryParse(memory, out var value))
            {
                return value * multiplier;
            }

            return null;
        }

        public async Task<List<Models.GameServer>> ListGameServersAsync()
        {
            logger.LogInformation("Fetching services from Docker Swarm...");
            var services = await client.Swarm.ListServicesAsync();
            logger.LogInformation("Converting services to GameServer list.");
            var servers = new List<Models.GameServer>();
            foreach (var svc in services)
            {
                var item = await TryCastGameServer(svc);
                if (item != null)
                {
                    servers.Add(item);
                }
            }
            logger.LogInformation($"Found {servers.Count} GameServers");
            return servers;
        }

        private async Task<Models.GameServer?> TryCastGameServer(SwarmService service)
        {
            if (!service.Spec.Labels.ContainsKey("gameserver.docker.managed") || service.Spec.Labels["gameserver.docker.managed"] != "true")
            {
                return null;
            }
            //Otherwise, we have a managed GameServer
            var item = new Models.GameServer();
            if (service.Spec.Labels.ContainsKey("gameserver.docker.Id"))
            {
                item.ServerId = service.Spec.Labels["gameserver.docker.Id"];
            }
            if (service.Spec.Labels.ContainsKey("gameserver.docker.name"))
            {
                item.Name = service.Spec.Labels["gameserver.docker.name"];
            }
            if (service.Spec.Labels.ContainsKey("gameserver.docker.description"))
            {
                item.Description = service.Spec.Labels["gameserver.docker.description"];
            }
            if (service.Spec.Labels.ContainsKey("gameserver.docker.gametype"))
            {
                item.GameType = service.Spec.Labels["gameserver.docker.gametype"];
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
            var tasks = await GetTasksForSwarmServiceAsync(service.ID);
            
            // Get desired replicas from service spec
            var desiredReplicas = (int)(service.Spec?.Mode?.Replicated?.Replicas ?? 0);
            
            // Count running tasks
            var runningTasks = tasks.Count(t => t.Status?.State == TaskState.Running);
            
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
            logger.LogInformation("Fetching services from Docker Swarm...");
            var services = await client.Swarm.ListServicesAsync();
            logger.LogInformation("Converting services to GameServer list.");
            foreach (var svc in services)
            {
                var item = await TryCastGameServer(svc);
                if (item?.ServerId == Id)
                    return item;
            }
            return null;
        }

        public async Task CreateOrUpdateGameServerAsync(Models.GameServer server, Models.GameTypeDefinition definition, bool performShutdown = false)
        {
            var existing = await GetGameServerById(server.ServerId);
            if (existing == null)
            {
                logger.LogInformation($"Creating new GameServer: {server.Name} ({server.ServerId})");
                var serviceSpec = await BuildGameServerServiceSpec(server, definition);

                await client.Swarm.CreateServiceAsync(new ServiceCreateParameters
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
                    Name = new[] { existing.ServiceName }
                };

                var services = await client.Swarm.ListServicesAsync(new ServicesListParameters
                {
                    Filters = serviceFilter
                });

                if (!services.Any())
                {
                    logger.LogError("Failed to find existing service for update.");
                    throw new InvalidOperationException($"Existing service '{existing.ServiceName}' not found for update.");
                }

                var service = services.First();

                // Build updated spec from the new configuration, passing existing spec for reference
                var updatedSpec = await BuildGameServerServiceSpec(server, definition, service.Spec, performShutdown);

                // Update the service with correct version
                logger.LogDebug($"Updating service {service.ID} with version {service.Version.Index}");

                await client.Swarm.UpdateServiceAsync(
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

            var logsParams = new ServiceLogsParameters
            {
                Follow = false,
                ShowStdout = true,
                ShowStderr = true,
                Timestamps = true,
                Tail = tailLines.ToString()
            };

            try
            {
                var logStream = await client.Swarm.GetServiceLogsAsync(serviceId, logsParams, CancellationToken.None);
                var logLines = new List<string>();

                using var reader = new StreamReader(logStream);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    // Docker logs come with 8-byte header (stream type + size), skip it if present
                    if (line.Length > 8 && (line[0] == 1 || line[0] == 2))
                    {
                        logLines.Add(line[8..]);
                    }
                    else
                    {
                        logLines.Add(line);
                    }
                }

                logger.LogInformation($"Retrieved {logLines.Count} log lines for server {serverId}");
                return logLines;
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

            var services = await client.Swarm.ListServicesAsync();

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
            return await client.Swarm.InspectServiceAsync(serviceId);
        }

        public async Task<List<TaskResponse>> GetTasksForSwarmServiceAsync(string serviceId)
        {
            // Query tasks from Swarm Manager only
            var filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["service"] = new Dictionary<string, bool> { [serviceId] = true }
            };

            var allTasks = await client.Tasks.ListAsync(new TasksListParameters { Filters = filters });

            return allTasks.ToList();
        }

        public async Task<string> GetRunningContainerIdForGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            if (server == null)
                throw new InvalidOperationException($"Server {serverId} not found");

            var serviceDetails = await client.Swarm.ListServicesAsync(new ServicesListParameters
            {
                Filters = new ServiceFilter
                {
                    Name = [server.ServiceName]
                }
            });

            var service = serviceDetails.FirstOrDefault();
            if (service == null)
                throw new InvalidOperationException($"Unable to locate Service by name {server.ServiceName}");

            // Find running task/container for this service
            var tasks = await client.Tasks.ListAsync();

            var runningTask = tasks
                .Where(t => t.ServiceID == service.ID && t.Status?.State == TaskState.Running)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            return runningTask?.Status?.ContainerStatus?.ContainerID ?? "";
        }

        internal async Task StartGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            var definition = await gameTypeRepository.GetByKeyAsync(server!.GameType);
            if (definition == null)
            {
                throw new ArgumentException($"Unable to locate gameType {server.GameType}");
            }
            await CreateOrUpdateGameServerAsync(server, definition);
        }

        internal async Task StopGameServerAsync(string serverId)
        {
            var server = await GetGameServerById(serverId);
            var definition = await gameTypeRepository.GetByKeyAsync(server!.GameType);
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
                    await client.Swarm.RemoveServiceAsync(server.ServiceName);
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
    }
}

