# Startup Performance Troubleshooting Guide

## Quick Diagnostics

### 1. Check Startup Timing
Look for these log entries to identify bottlenecks:

```log
[HH:MM:SS] Starting GameServer.Docker Version - X.X.X.X
[HH:MM:SS] 🚀 WebHost built successfully. Starting database initialization...
[HH:MM:SS] Initializing database...
[HH:MM:SS] Database initialized. Found N game types.
[HH:MM:SS] ✅ Database initialization complete. Starting webhost...
[HH:MM:SS] Now listening on: http://0.0.0.0:8080
```

**Normal timing**:
- WebHost build: 2-5 seconds
- Database init: 1-3 seconds
- Total startup: 5-10 seconds

**Slow startup indicators**:
- Gap > 10s between "Starting" and "WebHost built" → Service registration issue
- Gap > 5s between "Starting database initialization" and "Database initialized" → Database performance issue
- Gap > 2s between "Database initialized" and "Now listening" → Middleware/routing configuration issue

### 2. Common Bottlenecks

#### A. EF Core Service Provider Validation
**Symptom**: Long delay during `app.Build()`
**Fix**: Ensure `EnableServiceProviderCaching(false)` is set in `AddDbContext()`

#### B. Database File I/O
**Symptom**: Long delay during "Initializing database..."
**Solutions**:
- Ensure `/data` volume is mounted correctly
- Check disk I/O performance on host
- Verify SQLite WAL mode is enabled
- Consider using tmpfs for database in development

#### C. Network Resolution Delays
**Symptom**: DNS resolution timeouts in agent logs
**Fix**: Ensure Docker Swarm service names are resolvable

### 3. Performance Metrics

#### Measure `app.Build()` Time
Add logging around the build call:

```csharp
var buildStart = DateTime.UtcNow;
var app = builder.Build();
var buildTime = (DateTime.UtcNow - buildStart).TotalSeconds;
mainLogger.LogInformation($"app.Build() took {buildTime:F2}s");
```

#### Measure Database Init Time
Already logged in `GameTypeRepository.InitializeDatabaseAsync()`:

```log
[18:01:33] Initializing database...
[18:01:34] Database initialized. Found 8 game types.
```
Timing = difference between these two timestamps

### 4. Environment-Specific Issues

#### Docker Swarm Overlay Network
- First connection to a service on overlay network can be slow
- Subsequent connections are fast due to cached routes
- **Solution**: Use health checks with retries

#### Volume Mount Performance
- NFS/network volumes can be slow for database files
- **Solution**: Use local volumes or tmpfs for development
- **Production**: Use volume driver optimized for databases

#### Container Resource Limits
- Memory limits can cause swapping
- CPU limits can slow down startup
- **Check**: Docker service resource constraints

```bash
docker service inspect gameserver-docker_gameserver-docker --format '{{.Spec.TaskTemplate.Resources}}'
```

### 5. Debug Mode Checks

Enable detailed logging:

```bash
# Set environment variable
SKIP_DB_INIT=false
ASPNETCORE_ENVIRONMENT=Development

# Check logs
docker service logs -f gameserver-docker_gameserver-docker
```

### 6. SQLite-Specific Optimizations

Verify connection string has these optimizations:

```csharp
var optimizedConnectionString = new SqliteConnectionStringBuilder(connectionString)
{
    Mode = SqliteOpenMode.ReadWriteCreate,  // ✅ Fast mode
    Cache = SqliteCacheMode.Shared,         // ✅ Enable shared cache
    Pooling = true                          // ✅ Connection pooling
}.ToString();
```

### 7. Comparison Table

| Metric | Target | Warning | Critical |
|--------|--------|---------|----------|
| `app.Build()` | < 5s | 5-10s | > 10s |
| Database Init | < 3s | 3-5s | > 5s |
| Total Startup | < 10s | 10-20s | > 20s |
| First Agent Connection | < 15s | 15-30s | > 30s |

### 8. Emergency Workarounds

#### Skip Database Initialization
If database init is blocking startup:

```bash
# Set environment variable in docker-compose.yml or service definition
SKIP_DB_INIT=true
```

Then manually initialize database after startup:
```bash
docker exec <container-id> dotnet GameServer.Docker.dll --no-db-init=false
```

#### Use In-Memory Database (Development Only)
Modify `Program.cs` temporarily:

```csharp
// Replace SQLite with In-Memory
builder.Services.AddDbContext<GameServerDbContext>(options =>
{
    options.UseInMemoryDatabase("GameServerDb");
});
```

**WARNING**: Data is lost when container restarts!

### 9. Monitoring Commands

```bash
# Watch logs with timestamps
docker service logs -f --timestamps gameserver-docker_gameserver-docker

# Check service health
docker service ps gameserver-docker_gameserver-docker

# Check container resource usage
docker stats <container-id>

# Check volume mount performance
docker exec <container-id> dd if=/dev/zero of=/data/test.tmp bs=1M count=100
```

### 10. When to Escalate

Contact the development team if:
- Startup takes > 60 seconds consistently
- Database initialization fails repeatedly
- Agents cannot connect after 5 minutes
- Memory usage grows indefinitely during startup
- CPU usage stays at 100% during startup

## Quick Fixes Checklist

- [ ] Verify `/data` volume is mounted and writable
- [ ] Check Docker Swarm overlay network connectivity
- [ ] Ensure `EnableServiceProviderCaching(false)` is set
- [ ] Verify `EnableSensitiveDataLogging()` is only in Development
- [ ] Check database file isn't corrupted (delete and recreate)
- [ ] Verify container has sufficient memory (minimum 512MB)
- [ ] Check no other services are blocking port 8080
- [ ] Verify agent service can resolve DNS name of primary service

## Performance Regression Detection

If startup suddenly becomes slow after an update:

1. **Compare logs**: Check timing between old and new versions
2. **Check new dependencies**: New NuGet packages might add overhead
3. **Review recent changes**: Use git to see what changed in `Program.cs`
4. **Profile startup**: Use dotnet-trace to capture startup profile

```bash
# Install profiling tools
dotnet tool install --global dotnet-trace

# Capture startup trace
dotnet-trace collect --process-id <pid> --duration 00:00:30
```
