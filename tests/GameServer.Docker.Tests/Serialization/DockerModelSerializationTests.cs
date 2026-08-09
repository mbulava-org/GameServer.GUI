using Docker.DotNet.Models;
using GameServer.Docker.Constants;
using System.Text.Json;

namespace GameServer.Docker.Tests.Serialization;

/// <summary>
/// Tests to ensure Docker.DotNet models serialize/deserialize correctly through JSON.
/// These tests prevent regressions where complex nested objects lose data during JSON round-trips.
/// </summary>
public class DockerModelSerializationTests
{
    [Fact]
    public void SwarmService_SerializationRoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange - Create a realistic SwarmService with nested structure
        var originalService = new SwarmService
        {
            ID = "test-service-id-123",
            Version = new global::Docker.DotNet.Models.Version { Index = 456 },
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow,
            Spec = new ServiceSpec
            {
                Name = "minecraft-survival",
                Labels = new Dictionary<string, string>
                {
                    [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
                    [ServiceLabels.ServerId] = "minecraft-001",
                    [ServiceLabels.Name] = "Minecraft Survival Server",
                    [ServiceLabels.GameType] = "minecraft",
                    [ServiceLabels.Description] = "Main survival world"
                },
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = "minecraft:java21-alpine",
                        Env = new List<string>
                        {
                            "EULA=true",
                            "DIFFICULTY=normal",
                            "MAX_PLAYERS=20"
                        },
                        Mounts = new List<Mount>
                        {
                            new()
                            {
                                Type = "volume",
                                Source = "minecraft-data",
                                Target = "/data"
                            }
                        }
                    },
                    RestartPolicy = new SwarmRestartPolicy
                    {
                        Condition = "on-failure",
                        MaxAttempts = 3
                    }
                },
                Mode = new ServiceMode
                {
                    Replicated = new ReplicatedService { Replicas = 1 }
                },
                EndpointSpec = new EndpointSpec
                {
                    Ports = new List<PortConfig>
                    {
                        new()
                        {
                            Protocol = "tcp",
                            TargetPort = 25565,
                            PublishedPort = 25565
                        }
                    }
                }
            },
            Endpoint = new Endpoint
            {
                Spec = new EndpointSpec(),
                Ports = new List<PortConfig>
                {
                    new()
                    {
                        Protocol = "tcp",
                        TargetPort = 25565,
                        PublishedPort = 25565
                    }
                }
            }
        };

        // Act - Serialize and deserialize (simulates HTTP transmission)
        var json = JsonSerializer.Serialize(originalService);
        var deserializedService = JsonSerializer.Deserialize<SwarmService>(json);

        // Assert - All critical properties must be preserved
        Assert.NotNull(deserializedService);
        Assert.Equal(originalService.ID, deserializedService.ID);
        Assert.Equal(originalService.Version.Index, deserializedService.Version.Index);

        // CRITICAL: Spec must not be null!
        Assert.NotNull(deserializedService.Spec);
        Assert.Equal("minecraft-survival", deserializedService.Spec.Name);

        // CRITICAL: Labels must be preserved!
        Assert.NotNull(deserializedService.Spec.Labels);
        Assert.Equal(5, deserializedService.Spec.Labels.Count);
        Assert.Equal("true", deserializedService.Spec.Labels[ServiceLabels.Managed]);
        Assert.Equal("minecraft-001", deserializedService.Spec.Labels[ServiceLabels.ServerId]);
        Assert.Equal("minecraft", deserializedService.Spec.Labels[ServiceLabels.GameType]);

        // CRITICAL: TaskTemplate must be preserved!
        Assert.NotNull(deserializedService.Spec.TaskTemplate);
        Assert.NotNull(deserializedService.Spec.TaskTemplate.ContainerSpec);
        Assert.Equal("minecraft:java21-alpine", deserializedService.Spec.TaskTemplate.ContainerSpec.Image);

        // Environment variables
        Assert.NotNull(deserializedService.Spec.TaskTemplate.ContainerSpec.Env);
        Assert.Equal(3, deserializedService.Spec.TaskTemplate.ContainerSpec.Env.Count);
        Assert.Contains("EULA=true", deserializedService.Spec.TaskTemplate.ContainerSpec.Env);

        // Mounts
        Assert.NotNull(deserializedService.Spec.TaskTemplate.ContainerSpec.Mounts);
        Assert.Single(deserializedService.Spec.TaskTemplate.ContainerSpec.Mounts);
        Assert.Equal("minecraft-data", deserializedService.Spec.TaskTemplate.ContainerSpec.Mounts[0].Source);
        Assert.Equal("/data", deserializedService.Spec.TaskTemplate.ContainerSpec.Mounts[0].Target);

        // RestartPolicy
        Assert.NotNull(deserializedService.Spec.TaskTemplate.RestartPolicy);
        Assert.Equal("on-failure", deserializedService.Spec.TaskTemplate.RestartPolicy.Condition);
        Assert.Equal(3UL, deserializedService.Spec.TaskTemplate.RestartPolicy.MaxAttempts);

        // Mode
        Assert.NotNull(deserializedService.Spec.Mode);
        Assert.NotNull(deserializedService.Spec.Mode.Replicated);
        Assert.Equal(1UL, deserializedService.Spec.Mode.Replicated.Replicas);

        // EndpointSpec
        Assert.NotNull(deserializedService.Spec.EndpointSpec);
        Assert.NotNull(deserializedService.Spec.EndpointSpec.Ports);
        Assert.Single(deserializedService.Spec.EndpointSpec.Ports);

        // Endpoint
        Assert.NotNull(deserializedService.Endpoint);
        Assert.NotNull(deserializedService.Endpoint.Ports);
        Assert.Single(deserializedService.Endpoint.Ports);
        Assert.Equal(25565u, deserializedService.Endpoint.Ports[0].PublishedPort);
    }

    [Fact]
    public void TaskResponse_SerializationRoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange
        var originalTask = new TaskResponse
        {
            ID = "task-123",
            ServiceID = "service-456",
            NodeID = "node-789",
            Status = new global::Docker.DotNet.Models.TaskStatus
            {
                Timestamp = DateTime.UtcNow,
                State = TaskState.Running,
                Message = "started",
                ContainerStatus = new ContainerStatus
                {
                    ContainerID = "container-abc123",
                    PID = 1234
                }
            },
            DesiredState = TaskState.Running,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(originalTask);
        var deserializedTask = JsonSerializer.Deserialize<TaskResponse>(json);

        // Assert
        Assert.NotNull(deserializedTask);
        Assert.Equal("task-123", deserializedTask.ID);
        Assert.Equal("service-456", deserializedTask.ServiceID);
        Assert.Equal("node-789", deserializedTask.NodeID);

        // CRITICAL: Status must be preserved!
        Assert.NotNull(deserializedTask.Status);
        Assert.Equal(TaskState.Running, deserializedTask.Status.State);
        Assert.Equal("started", deserializedTask.Status.Message);

        // CRITICAL: ContainerStatus must be preserved!
        Assert.NotNull(deserializedTask.Status.ContainerStatus);
        Assert.Equal("container-abc123", deserializedTask.Status.ContainerStatus.ContainerID);
        Assert.Equal(1234, deserializedTask.Status.ContainerStatus.PID);

        Assert.Equal(TaskState.Running, deserializedTask.DesiredState);
    }

    [Fact]
    public void NetworkResponse_SerializationRoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange
        var originalNetwork = new NetworkResponse
        {
            ID = "network-123",
            Name = "gameserver-network",
            Driver = "overlay",
            Scope = "swarm",
            IPAM = new IPAM
            {
                Driver = "default",
                Config = new List<IPAMConfig>
                {
                    new()
                    {
                        Subnet = "10.0.0.0/24",
                        Gateway = "10.0.0.1"
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(originalNetwork);
        var deserializedNetwork = JsonSerializer.Deserialize<NetworkResponse>(json);

        // Assert
        Assert.NotNull(deserializedNetwork);
        Assert.Equal("network-123", deserializedNetwork.ID);
        Assert.Equal("gameserver-network", deserializedNetwork.Name);
        Assert.Equal("overlay", deserializedNetwork.Driver);

        // CRITICAL: IPAM must be preserved!
        Assert.NotNull(deserializedNetwork.IPAM);
        Assert.NotNull(deserializedNetwork.IPAM.Config);
        Assert.Single(deserializedNetwork.IPAM.Config);
        Assert.Equal("10.0.0.0/24", deserializedNetwork.IPAM.Config[0].Subnet);
    }

    [Fact]
    public void SwarmService_WithNullSpec_ShouldSerializeWithoutError()
    {
        // Arrange - Edge case: service with null Spec
        var service = new SwarmService
        {
            ID = "service-id",
            Spec = null  // Null spec (shouldn't happen but defensive)
        };

        // Act
        var json = JsonSerializer.Serialize(service);
        var deserialized = JsonSerializer.Deserialize<SwarmService>(json);

        // Assert - Should handle gracefully
        Assert.NotNull(deserialized);
        Assert.Equal("service-id", deserialized.ID);
        Assert.Null(deserialized.Spec);  // Null should be preserved
    }

    [Fact]
    public void SwarmService_WithEmptyLabels_ShouldPreserveEmptyDictionary()
    {
        // Arrange
        var service = new SwarmService
        {
            ID = "service-id",
            Spec = new ServiceSpec
            {
                Name = "test-service",
                Labels = new Dictionary<string, string>()  // Empty labels
            }
        };

        // Act
        var json = JsonSerializer.Serialize(service);
        var deserialized = JsonSerializer.Deserialize<SwarmService>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Spec);
        Assert.NotNull(deserialized.Spec.Labels);
        Assert.Empty(deserialized.Spec.Labels);
    }
}
