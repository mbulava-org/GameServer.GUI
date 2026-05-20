# Service Lifetime Fix - Singleton vs Scoped

## Problem

Primary Service failed to start with:
```
System.InvalidOperationException: Cannot consume scoped service from singleton
```

## Root Cause

Several services were registered as **Singleton** but depended on **Scoped** services:

### Error 1-4: DockerServiceHelper Issue
`DockerServiceHelper` (Singleton) depended on:
- `IGameTypeRepository` (Scoped) ❌
- `DbContextOptions<GameServerDbContext>` (Scoped) ❌

Services that depended on `DockerServiceHelper`:
- `GameServerFileManagerService` (Singleton) ❌
- `GameServerManagerService` (Singleton) ❌  
- `GameServerResourceMonitorService` (Singleton) ❌

### Error 5: GameTypeMetadataApplier Issue
`GameTypeMetadataApplier` (Singleton) depended on:
- `IGameTypeRepository` (Scoped) ❌

## Solution

Changed the following services from **Singleton** to **Scoped**:

### Services Changed to Scoped

```csharp
// Before: Singleton ❌
// After: Scoped ✅

builder.Services.AddScoped<DockerServiceHelper>();
builder.Services.AddScoped<IGameServerFileManager, GameServerFileManagerService>();
builder.Services.AddScoped<IGameServerManager, GameServerManagerService>();
builder.Services.AddScoped<IGameServerResourceMonitor, GameServerResourceMonitorService>();
builder.Services.AddScoped<GameTypeMetadataApplier>();
```

## Service Lifetime Rules

### When to use Singleton
- ✅ Stateless services
- ✅ Thread-safe services
- ✅ No database dependencies
- ✅ Configuration/Options classes
- **Examples**: `ILogger<T>`, `IHttpClientFactory`, `IOptions<T>`

### When to use Scoped
- ✅ Services with database context
- ✅ Per-request state
- ✅ Entity Framework DbContext
- ✅ Repository pattern
- **Examples**: `DbContext`, `IGameTypeRepository`, services depending on DbContext

### When to use Transient
- ✅ Lightweight, stateless services
- ✅ Different instance each time
- **Examples**: Validators, temporary operations

## Dependency Chain

```
Scoped Services (per-request):
├── DbContext
│   └── DbContextOptions ← Registered as Scoped
└── IGameTypeRepository ← Uses DbContext

Scoped (because they depend on scoped):
├── DockerServiceHelper ← Depends on IGameTypeRepository
├── GameServerFileManagerService ← Depends on DockerServiceHelper
├── GameServerManagerService ← Depends on DockerServiceHelper
├── GameServerResourceMonitorService ← Depends on DockerServiceHelper
└── GameTypeMetadataApplier ← Depends on IGameTypeRepository

Singleton (no scoped dependencies):
├── IServiceOperations
├── ServiceOperationsViaAgent
├── ServiceOperationsViaDirect
├── WebHostResolver
├── IAgentRegistry
├── NodeAgentClient
└── PortAllocator
```

## Impact

### Performance
- ✅ **Minimal impact** - These services are already request-scoped in practice
- ✅ Scoped services are pooled per request (not per call)
- ✅ Controllers already create scopes automatically

### Memory
- ✅ **No significant change** - Scoped services are disposed after request
- ✅ DbContext connections properly managed

### Correctness
- ✅ **Fixed** - No more DI lifetime violations
- ✅ **Proper** - DbContext per request is the recommended pattern

## Testing

```bash
dotnet build
# ✅ Build successful

dotnet run
# ✅ Application starts without DI errors
```

## Files Modified

**File**: `src/GameServer.Docker/Program.cs`

**Changes**:
- Line 101: `DockerServiceHelper` → Scoped
- Line 105: `IGameServerFileManager` → Scoped
- Line 108: `IGameServerManager` → Scoped
- Line 151: `IGameServerResourceMonitor` → Scoped
- Line 206: `GameTypeMetadataApplier` → Scoped

## Best Practices

### ✅ DO
- Register DbContext as Scoped (default)
- Register services using DbContext as Scoped
- Use dependency injection properly

### ❌ DON'T
- Register Singleton services with DbContext dependencies
- Try to inject Scoped into Singleton
- Capture DbContext in long-lived objects

## Related Documentation

- [Microsoft Docs: Service Lifetimes](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#service-lifetimes)
- [EF Core: DbContext Lifetime](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

---

**Status**: ✅ **Fixed** - Application now starts successfully
