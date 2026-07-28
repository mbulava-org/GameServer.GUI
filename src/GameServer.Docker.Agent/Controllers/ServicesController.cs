using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Controller for Docker Swarm service management operations.
    /// Only available on manager nodes.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<ServicesController> _logger;

        public ServicesController(
            IDockerClient dockerClient,
            ILogger<ServicesController> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        /// <summary>
        /// Create a new Docker Swarm service
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ServiceOperationResponse>> CreateService([FromBody] CreateServiceRequest request)
        {
            try
            {
                _logger.LogInformation("Creating service: {ServiceName}", request.ServiceName);

                // Build service spec
                var serviceSpec = new ServiceCreateParameters
                {
                    Service = new ServiceSpec
                    {
                        Name = request.ServiceName,
                        TaskTemplate = new TaskSpec
                        {
                            ContainerSpec = new ContainerSpec
                            {
                                Image = request.Image,
                                Labels = request.Labels,
                                Env = request.Env.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                                Mounts = request.Mounts.Select(MapMountConfig).ToList()
                            },
                            Resources = request.Resources != null ? new ResourceRequirements
                            {
                                Limits = new SwarmLimit
                                {
                                    MemoryBytes = request.Resources.MemoryBytes ?? 0,
                                    NanoCPUs = request.Resources.NanoCPUs ?? 0
                                }
                            } : null,
                            RestartPolicy = request.RestartPolicy != null ? new SwarmRestartPolicy
                            {
                                Condition = request.RestartPolicy.Condition,
                                Delay = request.RestartPolicy.Delay.HasValue ? TimeSpan.FromTicks((long)request.RestartPolicy.Delay.Value / 100) : null,
                                MaxAttempts = request.RestartPolicy.MaxAttempts
                            } : null,
                            Placement = request.Placement != null ? new Placement
                            {
                                Constraints = request.Placement.Constraints
                            } : null,
                            Networks = request.Networks.Select(n => new NetworkAttachmentConfig { Target = n }).ToList()
                        },
                        EndpointSpec = request.Ports.Any() ? new EndpointSpec
                        {
                            Ports = request.Ports.Select(p => new PortConfig
                            {
                                TargetPort = p.TargetPort,
                                PublishedPort = p.PublishedPort ?? 0,
                                Protocol = p.Protocol,
                                PublishMode = p.PublishMode ?? "ingress"
                            }).ToList()
                        } : null,
                        Labels = request.Labels
                    }
                };

                var response = await _dockerClient.Swarm.CreateServiceAsync(serviceSpec);

                _logger.LogInformation("Service created successfully: {ServiceId}", response.ID);

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = response.ID,
                    Message = "Service created successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create service: {ServiceName}", request.ServiceName);
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to create service: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Update an existing Docker Swarm service
        /// </summary>
        [HttpPut("{serviceId}")]
        public async Task<ActionResult<ServiceOperationResponse>> UpdateService(
            string serviceId,
            [FromBody] UpdateServiceRequest request)
        {
            try
            {
                _logger.LogInformation("Updating service: {ServiceId}", serviceId);

                // Get current service to get version
                var service = await _dockerClient.Swarm.InspectServiceAsync(serviceId);
                var currentSpec = service.Spec;

                // Update fields if provided
                if (request.Image != null)
                {
                    currentSpec.TaskTemplate.ContainerSpec.Image = request.Image;
                }

                if (request.Labels != null)
                {
                    currentSpec.Labels = request.Labels;
                    currentSpec.TaskTemplate.ContainerSpec.Labels = request.Labels;
                }

                if (request.Env != null)
                {
                    currentSpec.TaskTemplate.ContainerSpec.Env = request.Env
                        .Select(kv => $"{kv.Key}={kv.Value}")
                        .ToList();
                }

                if (request.Mounts != null)
                {
                    await PrepareMountsAsync(request.Mounts).ConfigureAwait(false);
                    currentSpec.TaskTemplate.ContainerSpec.Mounts = request.Mounts
                        .Select(MapMountConfig)
                        .ToList();
                }

                if (request.Resources != null)
                {
                    currentSpec.TaskTemplate.Resources ??= new ResourceRequirements();
                    currentSpec.TaskTemplate.Resources.Limits = new SwarmLimit
                    {
                        MemoryBytes = request.Resources.MemoryBytes ?? 0,
                        NanoCPUs = request.Resources.NanoCPUs ?? 0
                    };
                }

                // Force update if requested
                if (request.ForceUpdate)
                {
                    currentSpec.TaskTemplate.ForceUpdate = service.Spec.TaskTemplate.ForceUpdate + 1;
                }

                var updateParams = new ServiceUpdateParameters
                {
                    Service = currentSpec,
                    Version = (long)service.Version.Index
                };

                await _dockerClient.Swarm.UpdateServiceAsync(serviceId, updateParams);

                _logger.LogInformation("Service updated successfully: {ServiceId}", serviceId);

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = serviceId,
                    Message = "Service updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update service: {ServiceId}", serviceId);
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to update service: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Delete a Docker Swarm service
        /// </summary>
        [HttpDelete("{serviceId}")]
        public async Task<ActionResult<ServiceOperationResponse>> DeleteService(string serviceId)
        {
            try
            {
                _logger.LogInformation("Deleting service: {ServiceId}", serviceId);

                await _dockerClient.Swarm.RemoveServiceAsync(serviceId);

                _logger.LogInformation("Service deleted successfully: {ServiceId}", serviceId);

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = serviceId,
                    Message = "Service deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete service: {ServiceId}", serviceId);
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to delete service: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// List all Docker Swarm services
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ServiceOperationResponse>> ListServices([FromQuery] string? labelFilter = null)
        {
            try
            {
                _logger.LogDebug("Listing services with filter: {Filter}", labelFilter ?? "none");

                var filters = new ServiceListParameters();

                if (!string.IsNullOrEmpty(labelFilter))
                {
                    filters.Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["label"] = new Dictionary<string, bool> { [labelFilter] = true }
                    };
                }

                var services = await _dockerClient.Swarm.ListServicesAsync(filters);
                var servicesList = services.ToList();

                _logger.LogDebug("Found {Count} services", servicesList.Count);

                // Log first service details for debugging
                if (servicesList.Count > 0)
                {
                    var first = servicesList[0];
                    _logger.LogWarning("📤 [Agent-ListServices] First service from Docker: ID={Id}, Spec={HasSpec}, SpecName={Name}, Labels={LabelCount}", 
                        first.ID, 
                        first.Spec != null, 
                        first.Spec?.Name ?? "NULL",
                        first.Spec?.Labels?.Count ?? 0);
                }

                var response = new ServiceOperationResponse
                {
                    Success = true,
                    Message = $"Found {servicesList.Count} services",
                    Data = new Dictionary<string, object>
                    {
                        // Return full SwarmService objects so they deserialize correctly
                        ["services"] = servicesList
                    }
                };

                // Log what we're about to send
                var responseJson = JsonSerializer.Serialize(response);
                _logger.LogWarning("📤 [Agent-ListServices] Sending response (first 500 chars): {Json}", 
                    responseJson.Length > 500 ? responseJson[..500] : responseJson);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list services");
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to list services: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get detailed information about a specific service
        /// </summary>
        [HttpGet("{serviceId}")]
        public async Task<ActionResult<ServiceOperationResponse>> InspectService(string serviceId)
        {
            try
            {
                _logger.LogDebug("Inspecting service: {ServiceId}", serviceId);

                var service = await _dockerClient.Swarm.InspectServiceAsync(serviceId);

                _logger.LogWarning("📤 [Agent-InspectService] Service from Docker: ID={Id}, Spec={HasSpec}, SpecName={Name}, Labels={LabelCount}",
                    service.ID,
                    service.Spec != null,
                    service.Spec?.Name ?? "NULL",
                    service.Spec?.Labels?.Count ?? 0);

                var response = new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = serviceId,
                    Message = "Service retrieved successfully",
                    Data = new Dictionary<string, object>
                    {
                        ["service"] = service
                    }
                };

                var responseJson = JsonSerializer.Serialize(response);
                _logger.LogWarning("📤 [Agent-InspectService] Sending response (first 500 chars): {Json}",
                    responseJson.Length > 500 ? responseJson[..500] : responseJson);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect service: {ServiceId}", serviceId);
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to inspect service: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get logs from a Docker Swarm service (aggregated from all replicas/tasks)
        /// Only available on manager nodes.
        /// </summary>
        [HttpGet("{serviceId}/logs")]
        public async Task<ActionResult<ServiceOperationResponse>> GetServiceLogs(
            string serviceId,
            [FromQuery] int tail = 1000)
        {
            try
            {
                _logger.LogInformation("Fetching last {TailLines} lines of service logs for service {ServiceId}", tail, serviceId);

                var logsParams = new ServiceLogsParameters
                {
                    Follow = false,
                    ShowStdout = true,
                    ShowStderr = true,
                    Timestamps = true,
                    Tail = tail.ToString()
                };

                var logStream = await _dockerClient.Swarm.GetServiceLogsAsync(serviceId, logsParams, CancellationToken.None);
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

                _logger.LogInformation("Successfully fetched {Count} log lines for service {ServiceId}", logLines.Count, serviceId);

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = serviceId,
                    Message = $"Retrieved {logLines.Count} log lines",
                    Data = new Dictionary<string, object>
                    {
                        ["logs"] = logLines
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get service logs for service: {ServiceId}", serviceId);
                return StatusCode(500, new ServiceOperationResponse
                {
                    Success = false,
                    Message = $"Failed to get service logs: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Creates host paths and applies ownership/permissions for bind-mount volumes before the
        /// container starts. Named volumes rely on the Docker volume driver options.
        /// </summary>
        private async Task PrepareMountsAsync(IEnumerable<MountConfig> mounts)
        {
            foreach (var mount in mounts.Where(m =>
                string.Equals(m.Type, "bind", StringComparison.OrdinalIgnoreCase)
                && m.InitMode != "none"))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(mount.Source) && !Directory.Exists(mount.Source))
                    {
                        Directory.CreateDirectory(mount.Source);
                        _logger.LogInformation("Created bind mount host directory: {Path}", mount.Source);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to create directory for bind mount: {Path}", mount.Source);
                }

                await ApplyOwnershipAndPermissionsAsync(mount).ConfigureAwait(false);
            }

            // Named volumes with explicit labels are handled through the Swarm volume driver.
            await Task.CompletedTask;
        }

        private async Task ApplyOwnershipAndPermissionsAsync(MountConfig mount)
        {
            if (string.IsNullOrWhiteSpace(mount.Source) || !Directory.Exists(mount.Source))
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(mount.Permissions)
                    && TryParseOctal(mount.Permissions, out var mode))
                {
                    if (OperatingSystem.IsLinux())
                    {
                        await ChmodAsync(mount.Source, mode).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping chmod on non-Linux host for {Path}", mount.Source);
                    }
                }

                if ((mount.OwnerUid.HasValue || mount.OwnerGid.HasValue) && OperatingSystem.IsLinux())
                {
                    await ChownAsync(mount.Source, mount.OwnerUid, mount.OwnerGid).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to apply ownership/permissions to {Path}", mount.Source);
            }
        }

        private static bool TryParseOctal(string text, out int value)
        {
            value = 0;
            foreach (var c in text)
            {
                if (c < '0' || c > '7')
                {
                    value = 0;
                    return false;
                }

                value = (value << 3) | (c - '0');
            }

            return true;
        }

        private async Task ChmodAsync(string path, int mode)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{Convert.ToString(mode, 8)} {path}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start chmod process.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"chmod failed: {error}");
            }
        }

        private async Task ChownAsync(string path, int? uid, int? gid)
        {
            var owner = uid.HasValue ? uid.Value.ToStringInvariant() : string.Empty;
            if (gid.HasValue)
            {
                owner += $":{gid.Value.ToStringInvariant()}";
            }

            if (string.IsNullOrEmpty(owner))
            {
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chown",
                Arguments = $"{owner} {path}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start chown process.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"chown failed: {error}");
            }
        }

        private static Mount MapMountConfig(MountConfig m)
        {
            return new Mount
            {
                Type = m.Type,
                Source = m.Source,
                Target = m.Target,
                ReadOnly = m.ReadOnly,
                VolumeOptions = m.VolumeOptions != null || !string.IsNullOrWhiteSpace(m.DriverName)
                    ? new VolumeOptions
                    {
                        DriverConfig = new Driver
                        {
                            Name = m.DriverName.NullIfEmpty()
                                ?? m.VolumeOptions?.GetValueOrDefault("driver", "local")
                                ?? "local",
                            Options = m.VolumeOptions?.Where(kv => kv.Key != "driver")
                                .ToDictionary(kv => kv.Key, kv => kv.Value)
                                ?? new Dictionary<string, string>()
                        }
                    }
                    : null
            };
        }
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        public static string ToStringInvariant(this int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
