using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Configurations;
using GameServer.Docker.Agent.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Docker.Agent.Tests.Services;

/// <summary>
/// Tests for AgentRegistrationService capability filtering.
/// Ensures worker nodes don't advertise manager-only capabilities.
/// </summary>
public class AgentRegistrationServiceCapabilityFilteringTests
{
    private readonly Mock<IDockerClient> _mockDockerClient;
    private readonly Mock<ILogger<AgentRegistrationService>> _mockLogger;
    private readonly Mock<ISwarmOperations> _mockSwarmOperations;

    public AgentRegistrationServiceCapabilityFilteringTests()
    {
        _mockDockerClient = new Mock<IDockerClient>();
        _mockLogger = new Mock<ILogger<AgentRegistrationService>>();
        _mockSwarmOperations = new Mock<ISwarmOperations>();

        _mockDockerClient.Setup(x => x.Swarm).Returns(_mockSwarmOperations.Object);
    }

    [Fact]
    public void FilterCapabilitiesByNodeRole_ManagerNode_ShouldRetainAllCapabilities()
    {
        // Arrange
        var allCapabilities = new List<string> { "logs", "exec", "stats", "attach", "services", "tasks", "nodes", "swarm" };
        var isManagerNode = true;

        // Use reflection to access private static method
        var method = typeof(AgentRegistrationService).GetMethod(
            "FilterCapabilitiesByNodeRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var result = (List<string>)method!.Invoke(null, new object[] { allCapabilities, isManagerNode })!;

        // Assert
        Assert.Equal(8, result.Count);
        Assert.Contains("logs", result);
        Assert.Contains("exec", result);
        Assert.Contains("stats", result);
        Assert.Contains("attach", result);
        Assert.Contains("services", result);  // Manager keeps these
        Assert.Contains("tasks", result);
        Assert.Contains("nodes", result);
        Assert.Contains("swarm", result);
    }

    [Fact]
    public void FilterCapabilitiesByNodeRole_WorkerNode_ShouldFilterOutManagerCapabilities()
    {
        // Arrange
        var allCapabilities = new List<string> { "logs", "exec", "stats", "attach", "services", "tasks", "nodes", "swarm" };
        var isManagerNode = false;

        // Use reflection to access private static method
        var method = typeof(AgentRegistrationService).GetMethod(
            "FilterCapabilitiesByNodeRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var result = (List<string>)method!.Invoke(null, new object[] { allCapabilities, isManagerNode })!;

        // Assert
        Assert.Equal(4, result.Count);
        
        // Worker keeps container capabilities
        Assert.Contains("logs", result);
        Assert.Contains("exec", result);
        Assert.Contains("stats", result);
        Assert.Contains("attach", result);
        
        // Worker loses manager capabilities
        Assert.DoesNotContain("services", result);
        Assert.DoesNotContain("tasks", result);
        Assert.DoesNotContain("nodes", result);
        Assert.DoesNotContain("swarm", result);
    }

    [Theory]
    [InlineData("logs", true, true)]      // Container capability - both nodes
    [InlineData("exec", true, true)]      // Container capability - both nodes
    [InlineData("stats", true, true)]     // Container capability - both nodes
    [InlineData("attach", true, true)]    // Container capability - both nodes
    [InlineData("services", true, false)] // Manager-only
    [InlineData("tasks", true, false)]    // Manager-only
    [InlineData("nodes", true, false)]    // Manager-only
    [InlineData("swarm", true, false)]    // Manager-only
    public void FilterCapabilitiesByNodeRole_ShouldFilterCapabilityByNodeType(
        string capability, 
        bool managerHasIt, 
        bool workerHasIt)
    {
        // Arrange
        var capabilities = new List<string> { capability };

        var method = typeof(AgentRegistrationService).GetMethod(
            "FilterCapabilitiesByNodeRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act - Manager
        var managerResult = (List<string>)method!.Invoke(null, new object[] { capabilities, true })!;
        
        // Act - Worker
        var workerResult = (List<string>)method!.Invoke(null, new object[] { capabilities, false })!;

        // Assert
        if (managerHasIt)
        {
            Assert.Contains(capability, managerResult);
        }
        else
        {
            Assert.DoesNotContain(capability, managerResult);
        }

        if (workerHasIt)
        {
            Assert.Contains(capability, workerResult);
        }
        else
        {
            Assert.DoesNotContain(capability, workerResult);
        }
    }

    [Fact]
    public void FilterCapabilitiesByNodeRole_CaseInsensitive_ShouldFilterCorrectly()
    {
        // Arrange - Mixed case capabilities
        var capabilities = new List<string> { "SERVICES", "Logs", "ExEc", "TASKS" };
        var isWorkerNode = false;

        var method = typeof(AgentRegistrationService).GetMethod(
            "FilterCapabilitiesByNodeRole",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var result = (List<string>)method!.Invoke(null, new object[] { capabilities, isWorkerNode })!;

        // Assert - Should filter out SERVICES and TASKS (case-insensitive)
        Assert.Equal(2, result.Count);
        Assert.Contains("Logs", result);
        Assert.Contains("ExEc", result);
        Assert.DoesNotContain("SERVICES", result);
        Assert.DoesNotContain("TASKS", result);
    }
}
