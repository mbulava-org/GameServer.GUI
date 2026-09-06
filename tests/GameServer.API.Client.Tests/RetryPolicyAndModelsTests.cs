using GameServer.API.Client.Models;
using GameServer.API.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace GameServer.API.Client.Tests;

public class RetryPolicyAndModelsTests
{
    [Fact]
    public void ContainerConsoleClient_RetryPolicy_ReturnsExpectedDelays()
    {
        // ContainerConsoleClient has a private RetryPolicy class that implements IRetryPolicy
        // We can test its behavior by instantiating ContainerConsoleClient and testing retry intervals
        var policyType = typeof(ContainerConsoleClient).GetNestedType("RetryPolicy", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(policyType);

        var policy = (IRetryPolicy)Activator.CreateInstance(policyType)!;

        var context0 = new RetryContext { PreviousRetryCount = 0 };
        var delay0 = policy.NextRetryDelay(context0);
        Assert.NotNull(delay0);
        Assert.Equal(TimeSpan.Zero, delay0.Value);

        var context1 = new RetryContext { PreviousRetryCount = 1 };
        var delay1 = policy.NextRetryDelay(context1);
        Assert.NotNull(delay1);
        Assert.Equal(TimeSpan.FromSeconds(2), delay1.Value);

        var context2 = new RetryContext { PreviousRetryCount = 2 };
        var delay2 = policy.NextRetryDelay(context2);
        Assert.NotNull(delay2);
        Assert.Equal(TimeSpan.FromSeconds(10), delay2.Value);

        var context3 = new RetryContext { PreviousRetryCount = 3 };
        var delay3 = policy.NextRetryDelay(context3);
        Assert.NotNull(delay3);
        Assert.Equal(TimeSpan.FromSeconds(30), delay3.Value);

        var context4 = new RetryContext { PreviousRetryCount = 4 };
        var delay4 = policy.NextRetryDelay(context4);
        Assert.Null(delay4);
    }

    [Fact]
    public void ContainerTerminalClient_RetryPolicy_ReturnsExpectedDelays()
    {
        var policyType = typeof(ContainerTerminalClient).GetNestedType("RetryPolicy", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(policyType);

        var policy = (IRetryPolicy)Activator.CreateInstance(policyType)!;

        var context0 = new RetryContext { PreviousRetryCount = 0 };
        var delay0 = policy.NextRetryDelay(context0);
        Assert.NotNull(delay0);
        Assert.Equal(TimeSpan.Zero, delay0.Value);

        var context1 = new RetryContext { PreviousRetryCount = 1 };
        var delay1 = policy.NextRetryDelay(context1);
        Assert.NotNull(delay1);
        Assert.Equal(TimeSpan.FromSeconds(2), delay1.Value);

        var context2 = new RetryContext { PreviousRetryCount = 2 };
        var delay2 = policy.NextRetryDelay(context2);
        Assert.NotNull(delay2);
        Assert.Equal(TimeSpan.FromSeconds(10), delay2.Value);

        var context3 = new RetryContext { PreviousRetryCount = 3 };
        var delay3 = policy.NextRetryDelay(context3);
        Assert.NotNull(delay3);
        Assert.Equal(TimeSpan.FromSeconds(30), delay3.Value);

        var context4 = new RetryContext { PreviousRetryCount = 4 };
        var delay4 = policy.NextRetryDelay(context4);
        Assert.Null(delay4);
    }

    [Fact]
    public void ResourceMonitoringClient_RetryPolicy_ReturnsExpectedDelays()
    {
        var policyType = typeof(ResourceMonitoringClient).GetNestedType("RetryPolicy", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(policyType);

        var policy = (IRetryPolicy)Activator.CreateInstance(policyType)!;

        var context0 = new RetryContext { PreviousRetryCount = 0 };
        var delay0 = policy.NextRetryDelay(context0);
        Assert.NotNull(delay0);
        Assert.Equal(TimeSpan.Zero, delay0.Value);

        var context1 = new RetryContext { PreviousRetryCount = 1 };
        var delay1 = policy.NextRetryDelay(context1);
        Assert.NotNull(delay1);
        Assert.Equal(TimeSpan.FromSeconds(2), delay1.Value);

        var context2 = new RetryContext { PreviousRetryCount = 2 };
        var delay2 = policy.NextRetryDelay(context2);
        Assert.NotNull(delay2);
        Assert.Equal(TimeSpan.FromSeconds(10), delay2.Value);

        var context3 = new RetryContext { PreviousRetryCount = 3 };
        var delay3 = policy.NextRetryDelay(context3);
        Assert.NotNull(delay3);
        Assert.Equal(TimeSpan.FromSeconds(30), delay3.Value);

        var context4 = new RetryContext { PreviousRetryCount = 4 };
        var delay4 = policy.NextRetryDelay(context4);
        Assert.Null(delay4);
    }

    [Fact]
    public void ProblemDetails_PropertiesAndDictionary_WorkCorrectly()
    {
        var problem = new ProblemDetails
        {
            Type = "https://example.com/probs/out-of-memory",
            Title = "Out of Memory",
            Status = 500,
            Detail = "Server exceeded allocated heap",
            Instance = "/servers/1/restart"
        };

        problem.AdditionalProperties["customKey"] = "customValue";

        Assert.Equal("https://example.com/probs/out-of-memory", problem.Type);
        Assert.Equal("Out of Memory", problem.Title);
        Assert.Equal(500, problem.Status);
        Assert.Equal("Server exceeded allocated heap", problem.Detail);
        Assert.Equal("/servers/1/restart", problem.Instance);
        Assert.Equal("customValue", problem.AdditionalProperties["customKey"]);
    }

    [Fact]
    public void GenericExceptionClasses_ConstructAndExposeProperties()
    {
        var details = new ProblemDetails { Title = "Forbidden", Status = 403 };

        var ex1 = new GameServersApiException<ProblemDetails>("Failed", 403, "Response text", new Dictionary<string, IEnumerable<string>>(), details, null);
        Assert.Equal(403, ex1.StatusCode);
        Assert.Equal("Response text", ex1.Response);
        Assert.Same(details, ex1.Result);

        var ex2 = new GameServersFilesApiException<ProblemDetails>("Failed", 404, "Not found", new Dictionary<string, IEnumerable<string>>(), details, null);
        Assert.Equal(404, ex2.StatusCode);
        Assert.Same(details, ex2.Result);

        var ex3 = new GameTypesApiException<ProblemDetails>("Failed", 400, "Bad request", new Dictionary<string, IEnumerable<string>>(), details, null);
        Assert.Equal(400, ex3.StatusCode);
        Assert.Same(details, ex3.Result);

        var ex4 = new MountTypeConfigApiException<ProblemDetails>("Failed", 409, "Conflict", new Dictionary<string, IEnumerable<string>>(), details, null);
        Assert.Equal(409, ex4.StatusCode);
        Assert.Same(details, ex4.Result);

        var ex5 = new PortApiException<ProblemDetails>("Failed", 503, "Unavailable", new Dictionary<string, IEnumerable<string>>(), details, null);
        Assert.Equal(503, ex5.StatusCode);
        Assert.Same(details, ex5.Result);
    }

    [Fact]
    public void HubResourceUsage_Properties_CanBeSetAndRead()
    {
        var usage = new HubResourceUsage
        {
            ServerId = "srv-1",
            CpuUsagePercent = 42.5,
            MemoryUsageBytes = 1024 * 1024 * 512,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 50.0,
            NetworkRxBytes = 5000,
            NetworkTxBytes = 10000,
            BlockReadBytes = 200,
            BlockWriteBytes = 400,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            Timestamp = DateTime.UtcNow
        };

        Assert.Equal("srv-1", usage.ServerId);
        Assert.Equal(42.5, usage.CpuUsagePercent);
        Assert.Equal(1024 * 1024 * 512, usage.MemoryUsageBytes);
        Assert.Equal(1024 * 1024 * 1024, usage.MemoryLimitBytes);
        Assert.Equal(50.0, usage.MemoryUsagePercent);
        Assert.Equal(5000, usage.NetworkRxBytes);
        Assert.Equal(10000, usage.NetworkTxBytes);
        Assert.Equal(200, usage.BlockReadBytes);
        Assert.Equal(400, usage.BlockWriteBytes);
        Assert.Equal("Running", usage.ServiceStatus);
    }
}
