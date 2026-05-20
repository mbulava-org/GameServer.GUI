# Bug Fix: Double Logging Issue

**Date:** March 23, 2026  
**Issue:** All log messages appearing twice in GameServer.Docker and GameServer.Web  
**Severity:** Medium (log noise, performance impact)  
**Status:** ✅ Fixed

---

## 📋 Problem Description

Every log message was appearing **twice** in the console output for GameServer.Docker:

```
[04:56:47 INF] [GameServer.Docker.Program] 🚀 WebHost built successfully. Configuring middleware...
[04:56:47 INF] [GameServer.Docker.Program] 🚀 WebHost built successfully. Configuring middleware...
```

This was happening for:
- ✅ **GameServer.Docker** (Primary Service)
- ✅ **GameServer.Web** (Blazor UI)
- ❌ **GameServer.Docker.Agent** (Not affected)

---

## 🔍 Root Cause

The issue was caused by **duplicate Serilog console sinks** being registered:

### GameServer.Docker

1. **appsettings.json** configured Serilog with `WriteTo.Console`
2. **Program.cs** also added `.WriteTo.Console()` in code
3. When using `.ReadFrom.Configuration()`, it loaded the sink from JSON **AND** added the sink from code
4. **Result:** Two console sinks = double logging

### GameServer.Web

1. **Program.cs** added `.WriteTo.Console()` in code
2. Default logging providers were NOT cleared
3. **Result:** Both Serilog and default .NET logging wrote to console = double logging

---

## ✅ Solution

### GameServer.Docker (`src/GameServer.Docker/Program.cs`)

**Added:**
```csharp
// Clear default logging providers to prevent duplicates
builder.Logging.ClearProviders();
```

**Removed duplicate console sink from code:**
```csharp
// Before (WRONG):
builder.Services.AddSerilog((services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ApplicationName", "GameServer.Docker")
        .WriteTo.Console(  // ❌ DUPLICATE!
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

// After (CORRECT):
builder.Services.AddSerilog((services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ApplicationName", "GameServer.Docker"));
        // Console sink is configured in appsettings.json - don't add it here!
```

**Why it works:**
- `appsettings.json` already has `Serilog.WriteTo.Console` configured
- `ReadFrom.Configuration()` loads that sink automatically
- No need to add it again in code

### GameServer.Web (`src/GameServer.Web/Program.cs`)

**Added:**
```csharp
// Clear default logging providers to prevent duplicates
builder.Logging.ClearProviders();
```

**Kept console sink in code** (since appsettings.json doesn't have Serilog config):
```csharp
builder.Services.AddSerilog((services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ApplicationName", "GameServer.Web")
        .WriteTo.Console(  // ✅ This is the ONLY sink
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));
```

**Why it works:**
- `appsettings.json` does NOT have Serilog configuration
- Console sink must be added in code
- `ClearProviders()` removes default .NET logging

---

## 🧪 Testing

### Before Fix
```
[04:56:47 INF] [GameServer.Docker.Program] Starting application...
[04:56:47 INF] [GameServer.Docker.Program] Starting application...
[04:56:47 INF] [GameServer.Docker.Services.DatabaseInitializationService] Database ready
[04:56:47 INF] [GameServer.Docker.Services.DatabaseInitializationService] Database ready
```

### After Fix
```
[04:56:47 INF] [GameServer.Docker.Program] Starting application...
[04:56:47 INF] [GameServer.Docker.Services.DatabaseInitializationService] Database ready
```

✅ **Each message appears only once!**

---

## 📚 Best Practices for Serilog Configuration

### Option 1: Configuration in appsettings.json (Recommended)

**appsettings.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

**Program.cs:**
```csharp
builder.Logging.ClearProviders();  // ⚠️ IMPORTANT!

builder.Services.AddSerilog((services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)  // Loads sinks from JSON
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());
        // ❌ DON'T add .WriteTo.Console() here!
```

### Option 2: Configuration in Code Only

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
  // ❌ NO Serilog section
}
```

**Program.cs:**
```csharp
builder.Logging.ClearProviders();  // ⚠️ IMPORTANT!

builder.Services.AddSerilog((services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(  // ✅ Add sinks in code
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));
```

### ⚠️ Common Mistakes

**❌ DON'T:**
- Configure console sink in BOTH appsettings.json AND Program.cs
- Forget to call `builder.Logging.ClearProviders()`
- Mix Serilog with default .NET logging

**✅ DO:**
- Choose ONE place for sink configuration (config file OR code)
- Always call `ClearProviders()` when using Serilog
- Use `.ReadFrom.Configuration()` to load from appsettings.json

---

## 🔄 Files Changed

### Modified Files
- `src/GameServer.Docker/Program.cs`
  - Added `builder.Logging.ClearProviders()`
  - Removed duplicate `.WriteTo.Console()` call
  - Added explanatory comments

- `src/GameServer.Web/Program.cs`
  - Added `builder.Logging.ClearProviders()`
  - Added explanatory comments

### Configuration Files (No Changes)
- `src/GameServer.Docker/appsettings.json` - Already correct
- `src/GameServer.Web/appsettings.json` - Already correct
- `src/GameServer.Docker.Agent/Program.cs` - Not affected (uses `UseSerilog()`)

---

## 📊 Impact

### Performance
- **Before:** 2x log writes = 2x I/O overhead
- **After:** 1x log writes = normal performance
- **Improvement:** ~50% reduction in logging I/O

### Log Volume
- **Before:** Every message duplicated = 2x log file size
- **After:** Single messages only
- **Improvement:** 50% reduction in log volume

### Developer Experience
- **Before:** Confusing duplicate messages
- **After:** Clean, readable logs
- **Improvement:** Much easier to debug!

---

## ✅ Verification Checklist

- [x] Build succeeds without errors
- [x] GameServer.Docker logs appear once
- [x] GameServer.Web logs appear once
- [x] GameServer.Docker.Agent logs still work (unaffected)
- [x] All three services tested in Docker Swarm
- [x] Documentation updated

---

## 📝 Notes

- This issue was introduced when Serilog was initially configured
- It went unnoticed because the logs were still functional, just duplicated
- The fix is backward compatible - no breaking changes
- Similar pattern used in other ASP.NET Core + Serilog projects

---

**Fixed by:** Copilot  
**Reviewed by:** [Pending]  
**Deployed:** [Pending]
