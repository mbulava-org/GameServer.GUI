using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.Mvc;

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
                                Mounts = request.Mounts.Select(m => new Mount
                                {
                                    Type = m.Type,
                                    Source = m.Source,
                                    Target = m.Target,
                                    ReadOnly = m.ReadOnly,
                                    VolumeOptions = m.VolumeOptions != null ? new VolumeOptions
                                    {
                                        DriverConfig = new Driver
                                        {
                                            Name = m.VolumeOptions.GetValueOrDefault("driver", "local"),
                                            Options = m.VolumeOptions.Where(kv => kv.Key != "driver")
                                                .ToDictionary(kv => kv.Key, kv => kv.Value)
                                        }
                                    } : null
                                }).ToList()
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
                                Delay = request.RestartPolicy.Delay.HasValue ? (long)request.RestartPolicy.Delay.Value : null,
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

                var filters = new ServicesListParameters();

                if (!string.IsNullOrEmpty(labelFilter))
                {
                    filters.Filters = new ServiceFilter
                    {
                        Label = new[] { labelFilter }
                    };
                }

                var services = await _dockerClient.Swarm.ListServicesAsync(filters);
                var servicesList = services.ToList();

                _logger.LogDebug("Found {Count} services", servicesList.Count);

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    Message = $"Found {servicesList.Count} services",
                    Data = new Dictionary<string, object>
                    {
                        ["services"] = servicesList.Select(s => new
                        {
                            s.ID,
                            s.Spec.Name,
                            s.Spec.Labels,
                            s.Version,
                            s.CreatedAt,
                            s.UpdatedAt
                        }).ToList()
                    }
                });
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

                return Ok(new ServiceOperationResponse
                {
                    Success = true,
                    ServiceId = serviceId,
                    Message = "Service retrieved successfully",
                    Data = new Dictionary<string, object>
                    {
                        ["service"] = service
                    }
                });
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
    }
}
