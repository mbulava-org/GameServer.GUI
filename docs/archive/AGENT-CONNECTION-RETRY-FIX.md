# Agent Connection Retry Fix - Summary

## 🐛 Problem

**Agents crash immediately on startup if Primary Service isn't ready:**

```
[23:45:56 ERR] Failed to connect to Primary Service at http://gameserver-docker:8080
System.Net.Http.HttpRequestException: Connection refused (gameserver-docker:8080)
[23:45:56 FTL] BackgroundService failed
```

**Root Cause:** In Docker Swarm, services start asynchronously. Agents may start before the Primary Service is ready to accept connections.

---

## ✅ Solution

**Added exponential backoff retry logic at startup:**

### Key Changes

1. **New Method:** `ConnectAndRegisterWithRetryAsync()`
   - Wraps `ConnectAndRegisterAsync()` with retry logic
   - Implements exponential backoff
   - Gracefully handles cancellation

2. **Retry Strategy:**
   - **Max Retries:** 30 attempts (configurable)
   - **Base Delay:** 5 seconds (configurable)
   - **Backoff Formula:** `delay * 1.5^(attempt-1)`
   - **Max Delay:** 60 seconds per attempt
   - **Total Window:** ~5-10 minutes

3. **New Configuration Options:**
```csharp
public int MaxStartupRetries { get; set; } = 30;
public int StartupRetryDelaySeconds { get; set; } = 5;
```

### Example Retry Timeline

| Attempt | Delay | Cumulative Time |
|---------|-------|-----------------|
| 1 | 5s | 5s |
| 2 | 7.5s | 12.5s |
| 3 | 11.25s | 23.75s |
| 4 | 16.87s | 40.62s |
| 5 | 25.31s | 65.93s (1m 5s) |
| 10 | 60s (capped) | ~5 minutes |
| 20 | 60s (capped) | ~10 minutes |
| 30 | 60s (capped) | ~15 minutes |

---

## 📊 Behavior

### Before
```
Agent starts → Connect fails → CRASH (immediate)
```

### After
```
Agent starts → Connect fails → Wait 5s → Retry
             → Connect fails → Wait 7.5s → Retry
             → Connect fails → Wait 11.25s → Retry
             ...
             → Connect succeeds → Continue normally
```

### Logging
```
[23:45:56 WRN] Failed to connect to Primary Service (attempt 1/30). Retrying in 5s...
[23:46:01 WRN] Failed to connect to Primary Service (attempt 2/30). Retrying in 7.5s...
...
[23:46:23 INF] Successfully connected and registered with Primary Service
```

---

## 🎯 Benefits

1. **Resilient Startup:** Agents survive Primary Service delays
2. **Docker Swarm Friendly:** Handles async service startup
3. **Configurable:** Tune retries and delays per environment
4. **Graceful:** Respects cancellation tokens
5. **Logged:** Clear visibility into retry behavior
6. **Production-Ready:** Reasonable defaults, no infinite loops

---

## ⚙️ Configuration

### Default (Recommended for Production)
```json
{
  "AgentRegistration": {
    "PrimaryServiceUrl": "http://gameserver-docker:8080",
    "HeartbeatIntervalSeconds": 30,
    "MaxStartupRetries": 30,
    "StartupRetryDelaySeconds": 5
  }
}
```

### Fast Retry (Development)
```json
{
  "AgentRegistration": {
    "MaxStartupRetries": 10,
    "StartupRetryDelaySeconds": 2
  }
}
```

### Unlimited Retries (Long-Running Systems)
```json
{
  "AgentRegistration": {
    "MaxStartupRetries": 0  // 0 or negative = unlimited
  }
}
```

---

## 🧪 Testing

### Unit Test Scenarios
1. ✅ **Success on First Try:** No retries needed
2. ✅ **Success After Retries:** Connects on attempt 3
3. ✅ **Max Retries Exceeded:** Throws after 30 attempts
4. ✅ **Cancellation During Delay:** Stops gracefully
5. ✅ **Exponential Backoff:** Delays increase correctly
6. ✅ **Max Delay Cap:** Delays don't exceed 60s

### Integration Test
```bash
# Start agent before Primary Service
docker service scale gameserver-agent=3
# Agents retry while waiting...

# Start Primary Service
docker service scale gameserver-docker=1
# Agents connect within seconds!
```

---

## 📝 Related

- **Issue:** Connection refused on agent startup
- **Component:** `GameServer.Docker.Agent`
- **File:** `src/GameServer.Docker.Agent/Services/AgentRegistrationService.cs`
- **Commit:** e803694

---

## 🚀 Deployment Notes

### Docker Compose
No changes needed - defaults work well.

### Kubernetes
May want to reduce retries since k8s has built-in readiness probes:
```yaml
env:
  - name: AgentRegistration__MaxStartupRetries
    value: "10"
  - name: AgentRegistration__StartupRetryDelaySeconds
    value: "3"
```

### Environment Variables
```bash
# Override via environment variables
AgentRegistration__MaxStartupRetries=20
AgentRegistration__StartupRetryDelaySeconds=3
```

---

## 💡 Future Enhancements

1. **Health Check Integration:** Wait for `/health` endpoint
2. **Service Discovery:** Query Swarm for service readiness
3. **Circuit Breaker:** Stop retrying if service is down permanently
4. **Metrics:** Track retry attempts and success rates
5. **Dynamic Delays:** Adjust based on network latency

---

**Status:** ✅ Fixed and committed  
**Ready for Production:** Yes  
**Breaking Changes:** None (backward compatible)
