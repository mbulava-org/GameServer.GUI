# GameServer.Docker.Client - Complete Integration Examples

## Overview

This document provides comprehensive examples for integrating the GameServer.Docker.Client library into your application, covering both REST API clients and SignalR real-time features.

---

## Table of Contents

1. [Basic Setup](#basic-setup)
2. [Dependency Injection (Recommended)](#dependency-injection-recommended)
3. [REST API Examples](#rest-api-examples)
4. [SignalR Real-Time Examples](#signalr-real-time-examples)
5. [Complete Application Examples](#complete-application-examples)

---

## Basic Setup

### Install Package

```bash
dotnet add package GameServer.Docker.Client
```

### Package Dependencies

The client library includes:
- ? `Microsoft.AspNetCore.SignalR.Client` - For real-time features
- ? `Microsoft.Extensions.Http` - For HttpClient integration
- ? `Newtonsoft.Json` - For JSON serialization
- ? Auto-generated REST API clients (via NSwag)

---

## Dependency Injection (Recommended)

### Option 1: Register Everything at Once

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register all clients (REST API + SignalR)
builder.Services.AddGameServerClients(
    apiBaseUrl: "https://your-api.com",
    consoleHubUrl: "https://your-api.com/hubs/console",
    resourcesHubUrl: "https://your-api.com/hubs/resources"
);

var app = builder.Build();
```

### Option 2: Register with Custom Configuration

```csharp
using GameServer.Docker.Client.Extensions;
using Microsoft.AspNetCore.SignalR.Client;

var builder = WebApplication.CreateBuilder(args);

// Register with custom configuration
builder.Services.AddGameServerClients(
    apiBaseUrl: "https://your-api.com",
    configureHttpClient: client =>
    {
        // Add authentication, headers, etc.
        client.DefaultRequestHeaders.Add("X-API-Key", "your-api-key");
        client.Timeout = TimeSpan.FromSeconds(120);
    },
    consoleHubUrl: "https://your-api.com/hubs/console",
    configureConsoleHub: hubBuilder =>
    {
        hubBuilder
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) })
            .WithUrl("https://your-api.com/hubs/console", options =>
            {
                // Add authentication
                options.AccessTokenProvider = () => Task.FromResult("your-jwt-token");
            });
    },
    resourcesHubUrl: "https://your-api.com/hubs/resources",
    configureResourcesHub: hubBuilder =>
    {
        hubBuilder.WithAutomaticReconnect();
    }
);

var app = builder.Build();
```

### Option 3: Register Individually

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register REST API clients only
builder.Services.AddGameServerApiClients("https://your-api.com");

// Register SignalR clients separately
builder.Services.AddContainerConsoleClient("https://your-api.com/hubs/console");
builder.Services.AddResourceMonitoringClient("https://your-api.com/hubs/resources");

var app = builder.Build();
```

---

## REST API Examples

### Using Auto-Generated API Clients

The REST API clients are auto-generated from the OpenAPI specification at build time.

#### In a Controller

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class GameServersController : ControllerBase
{
    private readonly IGameServerApi _gameServerApi;
    private readonly IDashboardApi _dashboardApi;
    private readonly ILogger<GameServersController> _logger;

    public GameServersController(
        IHttpClientFactory httpClientFactory,
        ILogger<GameServersController> logger)
    {
        var httpClient = httpClientFactory.CreateClient("GameServer.Docker.Api");
        
        // Create API clients using the configured HttpClient
        _gameServerApi = new GameServerApi(httpClient);
        _dashboardApi = new DashboardApi(httpClient);
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        try
        {
            var servers = await _gameServerApi.ListAsync();
            return Ok(servers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching servers");
            return StatusCode(500, "Error fetching servers");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeployServer([FromBody] GameServer server)
    {
        try
        {
            await _gameServerApi.DeployAsync(server);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying server");
            return StatusCode(500, "Error deploying server");
        }
    }
}
```

#### In a Background Service

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ServerMonitoringService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServerMonitoringService> _logger;

    public ServerMonitoringService(
        IHttpClientFactory httpClientFactory,
        ILogger<ServerMonitoringService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpClient = _httpClientFactory.CreateClient("GameServer.Docker.Api");
        var api = new GameServerApi(httpClient);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var servers = await api.ListAsync();
                foreach (var server in servers)
                {
                    var resources = await api.GetResourceUsageAsync(server.ServerId);
                    _logger.LogInformation(
                        "Server {ServerId}: {Replicas}/{Desired} replicas, Health: {Health}%",
                        server.ServerId,
                        resources.RunningReplicas,
                        resources.DesiredReplicas,
                        resources.ReplicaHealthPercent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring servers");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

---

## SignalR Real-Time Examples

### Resource Monitoring Service

```csharp
using GameServer.Docker.Client.Interfaces;
using Microsoft.Extensions.Logging;

public class ResourceMonitoringService : IHostedService
{
    private readonly IResourceMonitoringClient _monitoringClient;
    private readonly ILogger<ResourceMonitoringService> _logger;

    public ResourceMonitoringService(
        IResourceMonitoringClient monitoringClient,
        ILogger<ResourceMonitoringService> logger)
    {
        _monitoringClient = monitoringClient;
        _logger = logger;

        // Setup event handlers
        _monitoringClient.ResourceUpdateReceived += OnResourceUpdate;
        _monitoringClient.ErrorReceived += OnError;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _monitoringClient.ConnectAsync(cancellationToken);
        
        // Subscribe to multiple servers
        await _monitoringClient.SubscribeToMultipleServersAsync(
            new[] { "minecraft-1", "valheim-1", "terraria-1" },
            intervalSeconds: 5);

        _logger.LogInformation("Resource monitoring started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _monitoringClient.UnsubscribeAsync(cancellationToken);
        await _monitoringClient.DisposeAsync();
    }

    private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
    {
        if (usage.RealTimeStats != null)
        {
            _logger.LogInformation(
                "Server {ServerId}: CPU {Cpu:F2}%, Memory {Memory:F2}%, Network RX: {RX} MB",
                usage.ServerId,
                usage.RealTimeStats.CpuUsagePercent,
                usage.RealTimeStats.MemoryUsagePercent,
                usage.RealTimeStats.NetworkRxBytes / 1024 / 1024);

            // Trigger alerts if needed
            if (usage.RealTimeStats.CpuUsagePercent > 90)
            {
                _logger.LogWarning("HIGH CPU USAGE on {ServerId}: {Cpu:F2}%",
                    usage.ServerId, usage.RealTimeStats.CpuUsagePercent);
            }
        }
    }

    private void OnError(object? sender, string error)
    {
        _logger.LogError("Resource monitoring error: {Error}", error);
    }
}
```

### Interactive Console Service

```csharp
using GameServer.Docker.Client.Interfaces;
using System.Collections.Concurrent;

public class ConsoleSessionManager
{
    private readonly IContainerConsoleClient _consoleClient;
    private readonly ILogger<ConsoleSessionManager> _logger;
    private readonly ConcurrentDictionary<string, List<string>> _outputBuffers = new();

    public ConsoleSessionManager(
        IContainerConsoleClient consoleClient,
        ILogger<ConsoleSessionManager> logger)
    {
        _consoleClient = consoleClient;
        _logger = logger;

        // Setup event handlers
        _consoleClient.OutputReceived += OnOutputReceived;
        _consoleClient.ErrorReceived += OnErrorReceived;
        _consoleClient.Connected += OnConnected;
        _consoleClient.Disconnected += OnDisconnected;
    }

    public async Task<bool> StartSessionAsync(string containerId)
    {
        await _consoleClient.ConnectAsync();
        
        _outputBuffers[containerId] = new List<string>();
        
        return await _consoleClient.AttachToContainerAsync(containerId);
    }

    public async Task SendCommandAsync(string command)
    {
        if (!string.IsNullOrEmpty(_consoleClient.AttachedContainerId))
        {
            await _consoleClient.SendInputAsync(command + "\n");
        }
    }

    public List<string> GetOutput(string containerId)
    {
        return _outputBuffers.TryGetValue(containerId, out var output) 
            ? new List<string>(output) 
            : new List<string>();
    }

    private void OnOutputReceived(object? sender, string output)
    {
        var containerId = _consoleClient.AttachedContainerId;
        if (containerId != null && _outputBuffers.TryGetValue(containerId, out var buffer))
        {
            buffer.Add(output);
            
            // Keep only last 1000 lines
            if (buffer.Count > 1000)
            {
                buffer.RemoveRange(0, buffer.Count - 1000);
            }
        }
    }

    private void OnErrorReceived(object? sender, string error)
    {
        _logger.LogError("Console error: {Error}", error);
    }

    private void OnConnected(object? sender, string containerId)
    {
        _logger.LogInformation("Connected to container {ContainerId}", containerId);
    }

    private void OnDisconnected(object? sender, string reason)
    {
        _logger.LogInformation("Disconnected: {Reason}", reason);
    }
}
```

---

## Complete Application Examples

### ASP.NET Core Web Application

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register GameServer.Docker clients
builder.Services.AddGameServerClients(
    apiBaseUrl: builder.Configuration["GameServer:ApiUrl"] ?? "https://localhost:7001",
    consoleHubUrl: builder.Configuration["GameServer:ConsoleHub"] ?? "https://localhost:7001/hubs/console",
    resourcesHubUrl: builder.Configuration["GameServer:ResourcesHub"] ?? "https://localhost:7001/hubs/resources"
);

// Register background services
builder.Services.AddHostedService<ResourceMonitoringService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Console Application

```csharp
using GameServer.Docker.Client;
using GameServer.Docker.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register GameServer.Docker clients
        services.AddGameServerClients(
            apiBaseUrl: "https://localhost:7001",
            consoleHubUrl: "https://localhost:7001/hubs/console",
            resourcesHubUrl: "https://localhost:7001/hubs/resources"
        );

        // Register application services
        services.AddHostedService<GameServerMonitorApp>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

await host.RunAsync();

// Application service
public class GameServerMonitorApp : BackgroundService
{
    private readonly IResourceMonitoringClient _monitoring;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GameServerMonitorApp> _logger;

    public GameServerMonitorApp(
        IResourceMonitoringClient monitoring,
        IHttpClientFactory httpClientFactory,
        ILogger<GameServerMonitorApp> logger)
    {
        _monitoring = monitoring;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Setup monitoring events
        _monitoring.ResourceUpdateBatchReceived += (s, updates) =>
        {
            foreach (var update in updates)
            {
                if (update.RealTimeStats != null)
                {
                    _logger.LogInformation(
                        "{Server}: CPU {Cpu:F1}%, Mem {Mem:F1}%",
                        update.ServerId,
                        update.RealTimeStats.CpuUsagePercent,
                        update.RealTimeStats.MemoryUsagePercent);
                }
            }
        };

        // Connect and subscribe
        await _monitoring.ConnectAsync(stoppingToken);

        // Get list of servers from REST API
        var httpClient = _httpClientFactory.CreateClient("GameServer.Docker.Api");
        var api = new GameServerApi(httpClient);
        var servers = await api.ListAsync();
        var serverIds = servers.Select(s => s.ServerId).ToArray();

        _logger.LogInformation("Monitoring {Count} servers", serverIds.Length);

        // Subscribe to all servers
        await _monitoring.SubscribeToMultipleServersAsync(serverIds, intervalSeconds: 5);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

### Blazor Server Application

```csharp
// Program.cs
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Register GameServer.Docker clients
builder.Services.AddGameServerClients(
    apiBaseUrl: "https://localhost:7001",
    consoleHubUrl: "https://localhost:7001/hubs/console",
    resourcesHubUrl: "https://localhost:7001/hubs/resources"
);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

```razor
@* Pages/ServerMonitor.razor *@
@page "/servers"
@using GameServer.Docker.Client.Interfaces
@implements IDisposable
@inject IResourceMonitoringClient MonitoringClient
@inject IHttpClientFactory HttpClientFactory

<h3>Server Monitor</h3>

@if (servers == null)
{
    <p>Loading...</p>
}
else
{
    <table class="table">
        <thead>
            <tr>
                <th>Server</th>
                <th>CPU</th>
                <th>Memory</th>
                <th>Network RX</th>
                <th>Network TX</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var server in servers)
            {
                <tr>
                    <td>@server.ServerId</td>
                    <td>@(server.RealTimeStats?.CpuUsagePercent.ToString("F2") ?? "N/A")%</td>
                    <td>@(server.RealTimeStats?.MemoryUsagePercent.ToString("F2") ?? "N/A")%</td>
                    <td>@FormatBytes(server.RealTimeStats?.NetworkRxBytes)</td>
                    <td>@FormatBytes(server.RealTimeStats?.NetworkTxBytes)</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<ServerResourceUsage> servers = new();

    protected override async Task OnInitializedAsync()
    {
        // Setup event handler
        MonitoringClient.ResourceUpdateBatchReceived += OnResourceUpdate;

        // Connect and subscribe
        await MonitoringClient.ConnectAsync();

        // Get server list and subscribe
        var httpClient = HttpClientFactory.CreateClient("GameServer.Docker.Api");
        var api = new GameServerApi(httpClient);
        var serverList = await api.ListAsync();
        var serverIds = serverList.Select(s => s.ServerId).ToArray();

        await MonitoringClient.SubscribeToMultipleServersAsync(serverIds, intervalSeconds: 5);
    }

    private void OnResourceUpdate(object? sender, IEnumerable<ServerResourceUsage> updates)
    {
        servers = updates.ToList();
        InvokeAsync(StateHasChanged);
    }

    private string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue) return "N/A";
        return $"{bytes.Value / 1024 / 1024:F2} MB";
    }

    public void Dispose()
    {
        MonitoringClient.ResourceUpdateBatchReceived -= OnResourceUpdate;
        MonitoringClient.UnsubscribeAsync().GetAwaiter().GetResult();
    }
}
```

---

## Configuration Examples

### appsettings.json

```json
{
  "GameServer": {
    "ApiUrl": "https://your-api.com",
    "ConsoleHub": "https://your-api.com/hubs/console",
    "ResourcesHub": "https://your-api.com/hubs/resources",
    "ApiKey": "your-api-key-here"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Information"
    }
  }
}
```

### Using Configuration

```csharp
builder.Services.AddGameServerClients(
    apiBaseUrl: builder.Configuration["GameServer:ApiUrl"]!,
    configureHttpClient: client =>
    {
        var apiKey = builder.Configuration["GameServer:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    },
    consoleHubUrl: builder.Configuration["GameServer:ConsoleHub"],
    resourcesHubUrl: builder.Configuration["GameServer:ResourcesHub"]
);
```

---

## Summary

The Client Library provides:

? **Easy Registration** - Simple extension methods for DI  
? **REST API Clients** - Auto-generated from OpenAPI  
? **SignalR Clients** - Real-time monitoring and console access  
? **Flexible Configuration** - Customize HttpClient and SignalR connections  
? **Complete Integration** - Works with ASP.NET Core, Console apps, Blazor, etc.  

All features work seamlessly together through dependency injection! ??
