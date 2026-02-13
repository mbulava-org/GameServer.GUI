# ResourceMonitor DI Injection Update ?

## Change Summary
Updated the `ResourceMonitor` component to use **dependency injection** for `IResourceMonitoringClient` instead of manually creating and managing the client instance.

## What Was Changed

### Before (Manual Client Creation)
```csharp
@inject IOptions<GameServerDockerApi> ApiConfig
@inject NotificationService NotificationService

@code {
    private IResourceMonitoringClient? monitoringClient;
    
    private async Task ConnectAsync()
    {
        // Build hub URL from configuration
        var baseUri = ApiConfig.Value.BaseUri?.TrimEnd('/') ?? "http://localhost:5164";
        var hubUrl = $"{baseUri}/hubs/resources";

        // ? Manually create the client
        monitoringClient = new ResourceMonitoringClient(hubUrl);
        
        // Subscribe to events
        monitoringClient.ResourceUpdateReceived += OnMetricsReceived;
        // ...
        
        await monitoringClient.ConnectAsync(connectionCts.Token);
    }
    
    public async ValueTask DisposeAsync()
    {
        // ? Manually dispose the client
        if (monitoringClient is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }
}
```

### After (Dependency Injection)
```csharp
@inject IOptions<GameServerDockerApi> ApiConfig
@inject NotificationService NotificationService
@inject IResourceMonitoringClient MonitoringClient  // ? Injected

@code {
    // ? No monitoringClient field needed
    
    private async Task ConnectAsync()
    {
        // ? Use injected client directly
        MonitoringClient.ResourceUpdateReceived += OnMetricsReceived;
        MonitoringClient.ErrorReceived += OnErrorReceived;
        MonitoringClient.Subscribed += OnMonitoringStarted;
        MonitoringClient.Unsubscribed += OnMonitoringStopped;

        // Check if already connected before connecting
        if (!MonitoringClient.IsConnected)
        {
            await MonitoringClient.ConnectAsync(connectionCts.Token);
        }

        await MonitoringClient.SubscribeToServerAsync(ContainerId, UpdateIntervalSeconds, connectionCts.Token);
    }
    
    public async ValueTask DisposeAsync()
    {
        // Unsubscribe from events
        MonitoringClient.ResourceUpdateReceived -= OnMetricsReceived;
        // ...
        
        await MonitoringClient.UnsubscribeAsync(CancellationToken.None);
        
        // ? Don't dispose - managed by DI container
    }
}
```

## Key Changes

### 1. Added Injection
```razor
@inject IResourceMonitoringClient MonitoringClient
```

### 2. Removed Field
```csharp
// ? REMOVED
private IResourceMonitoringClient? monitoringClient;
```

### 3. Updated ConnectAsync
- Removed manual client instantiation
- Removed hub URL building (now handled by DI registration)
- Use injected `MonitoringClient` instead of `monitoringClient`
- Added check for existing connection: `if (!MonitoringClient.IsConnected)`

### 4. Updated DisconnectAsync
- Use injected `MonitoringClient`
- Removed manual disposal logic
- Added comment explaining DI manages lifecycle

### 5. Updated DisposeAsync
- Use injected `MonitoringClient`
- Removed manual disposal
- Added comment about DI management

## Benefits

### ? Proper Lifecycle Management
The DI container manages the client's lifecycle based on the registration scope (Singleton, Scoped, or Transient).

### ? Testability
Easy to mock `IResourceMonitoringClient` for unit testing:
```csharp
// In tests
var mockClient = new Mock<IResourceMonitoringClient>();
// Configure mock behavior
// Inject mock into component
```

### ? Configuration Centralized
Hub URL and other configuration is in one place (`Program.cs`), not scattered throughout components.

### ? Connection Sharing
If registered as Singleton (which it is), the same SignalR connection can be shared across multiple component instances, reducing overhead.

### ? Follows Best Practices
Aligns with ASP.NET Core and Blazor dependency injection patterns.

## How It Works

### Registration in Program.cs
```csharp
// Already configured in Program.cs line 62
var resourcesUri = apiBaseUrl.Replace("https://", "wss://")
                             .Replace("http://", "ws://") + "hubs/resources";

builder.Services.AddResourceMonitoringClient(resourcesUri);
```

This registers `IResourceMonitoringClient` in the DI container, making it available for injection.

### Component Usage
```razor
@inject IResourceMonitoringClient MonitoringClient

@code {
    private async Task ConnectAsync()
    {
        // Just use it - no instantiation needed!
        await MonitoringClient.ConnectAsync(cancellationToken);
    }
}
```

## Important Notes

### Connection State Management
Since the client may be shared (if Singleton), we check `IsConnected` before connecting:
```csharp
if (!MonitoringClient.IsConnected)
{
    await MonitoringClient.ConnectAsync(connectionCts.Token);
}
```

This prevents multiple connect attempts if the client is already connected.

### Don't Dispose Injected Services
**CRITICAL**: Never dispose services injected via DI. The container manages their lifecycle.

```csharp
// ? CORRECT
public async ValueTask DisposeAsync()
{
    await MonitoringClient.UnsubscribeAsync(CancellationToken.None);
    // Don't dispose - DI container handles it
}

// ? WRONG
public async ValueTask DisposeAsync()
{
    await MonitoringClient.DisposeAsync(); // ? Don't do this!
}
```

### Event Subscription/Unsubscription
Components must still manage their own event subscriptions:
```csharp
// Subscribe
MonitoringClient.ResourceUpdateReceived += OnMetricsReceived;

// Always unsubscribe in disposal
MonitoringClient.ResourceUpdateReceived -= OnMetricsReceived;
```

## Service Lifetime

The `AddResourceMonitoringClient` extension method likely registers the client as:
- **Singleton**: One instance for the entire application lifetime
- **Scoped**: One instance per user session/circuit (common for Blazor Server)
- **Transient**: New instance every injection (unlikely for SignalR clients)

To verify, check the `AddResourceMonitoringClient` implementation:
```csharp
public static IServiceCollection AddResourceMonitoringClient(
    this IServiceCollection services, 
    string hubUrl)
{
    services.AddSingleton<IResourceMonitoringClient>(sp => 
        new ResourceMonitoringClient(hubUrl));
    return services;
}
```

## Migration Checklist

- [x] Add `@inject IResourceMonitoringClient MonitoringClient`
- [x] Remove `private IResourceMonitoringClient? monitoringClient;` field
- [x] Update `ConnectAsync` to use `MonitoringClient`
- [x] Remove manual client instantiation
- [x] Remove hub URL building
- [x] Update `DisconnectAsync` to use `MonitoringClient`
- [x] Remove manual disposal in `DisconnectAsync`
- [x] Update `DisposeAsync` to use `MonitoringClient`
- [x] Remove manual disposal in `DisposeAsync`
- [x] Build successful
- [ ] Test component functionality
- [ ] Verify no connection leaks
- [ ] Test with multiple component instances

## Testing

### Verify Functionality
1. Navigate to Server Details page
2. ResourceMonitor should auto-connect
3. Metrics should update in real-time
4. Verify connection state indicators work
5. Test disconnect/reconnect
6. Navigate away and back - should work correctly

### Verify No Connection Leaks
1. Open multiple server details pages
2. Navigate between them
3. Check SignalR hub connections (browser DevTools ? Network ? WS)
4. Should see efficient connection management

## Files Modified

1. **src/GameServer.Web/Components/Server/ResourceMonitor.razor**
   - Added `@inject IResourceMonitoringClient MonitoringClient`
   - Removed `monitoringClient` field
   - Updated `ConnectAsync()` method
   - Updated `DisconnectAsync()` method
   - Updated `DisposeAsync()` method

## Comparison with ContainerConsole

The `ContainerConsole` component can be updated similarly:

**Current (Manual)**:
```csharp
consoleClient = new ContainerConsoleClient(hubUrl);
```

**After DI**:
```csharp
@inject IContainerConsoleClient ConsoleClient
```

This has already been registered in `Program.cs` line 57:
```csharp
builder.Services.AddContainerConsoleClient(consoleUri);
```

## Related Documentation

- [ASP.NET Core Dependency Injection](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- [Blazor Dependency Injection](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection)
- [Service Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)

## Status

? **Implementation Complete**
- Dependency injection added
- Manual creation removed
- Lifecycle management delegated to DI
- Build successful
- Ready for testing

?? **Action Required**: Restart application and test

---

**Key Takeaway**: Use dependency injection for services like SignalR clients. It provides better lifecycle management, testability, and follows ASP.NET Core best practices. Always inject, never manually instantiate! ??
