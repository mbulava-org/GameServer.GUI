# Complete Diagnostic Output Guide - Version 0.0.4.218+

**Latest Commit:** 7e579ed  
**Diagnostic Level:** COMPREHENSIVE (All agent ↔ primary communication logged)  
**Tests:** 24 passing ✅

---

## 🎯 What's Logged Now

### Every Agent API Call Shows:

#### 📤 Agent Side (What Agent Sends)
```
[WRN] 📤 [Agent-ListServices] First service from Docker: ID=abc, Spec=True, SpecName=minecraft-server, Labels=5
[WRN] 📤 [Agent-ListServices] Sending response (first 500 chars): {"Success":true,"Message":"Found 13 services","Data":{"services":[{"ID":"abc",...
```

#### 📥 Primary Side (What Primary Receives)
```
[WRN] 📥 Raw JSON from agent (first 500 chars): {"Success":true,"Message":"Found 13 services","Data":{"services":[{"ID":"abc",...
[WRN] 📦 Services JSON (first 500 chars): [{"ID":"abc","Spec":{"Name":"minecraft-server","Labels":{"gameserver.docker.managed":"true"}...
[WRN] 🔍 First service: ID=abc, Spec=True, SpecName=minecraft-server ✅
```

---

## 📊 Complete Trace Example

### Successful ListServices Flow

```
=== AGENT SIDE ===
[INF] Listing services with filter: gameserver.docker.managed=true
[DBG] Found 2 services
[WRN] 📤 [Agent-ListServices] First service from Docker: 
      ID=minecraft-service-id, 
      Spec=True, 
      SpecName=minecraft-server, 
      Labels=5
[WRN] 📤 [Agent-ListServices] Sending response (first 500 chars): 
      {"Success":true,"Message":"Found 2 services","Data":{"services":[{"ID":"minecraft-service-id","Version":{"Index":123},"CreatedAt":"2026-03-01T00:00:00Z","UpdatedAt":"2026-03-04T00:00:00Z","Spec":{"Name":"minecraft-server","Labels":{"gameserver.docker.managed":"true","gameserver.docker.Id":"minecraft-001","gameserver.docker.name":"Minecraft Survival","gameserver.docker.gametype":"minecraft","gameserver.docker.description":"Main world"},"TaskTemplate":{"ContainerSpec":{"Image":"minecraft:latest"...
[INF] HTTP GET /api/services responded 200 in 45.2 ms

=== PRIMARY SIDE ===
[DBG] Listing services via agent on manager newdev-docker-001
[WRN] 📥 Raw JSON from agent (first 500 chars): 
      {"Success":true,"Message":"Found 2 services","Data":{"services":[{"ID":"minecraft-service-id","Version":{"Index":123},"CreatedAt":"2026-03-01T00:00:00Z","UpdatedAt":"2026-03-04T00:00:00Z","Spec":{"Name":"minecraft-server","Labels":{"gameserver.docker.managed":"true","gameserver.docker.Id":"minecraft-001","gameserver.docker.name":"Minecraft Survival","gameserver.docker.gametype":"minecraft","gameserver.docker.description":"Main world"},"TaskTemplate":{"ContainerSpec":{"Image":"minecraft:latest"...
[WRN] 📦 Services JSON (first 500 chars): 
      [{"ID":"minecraft-service-id","Version":{"Index":123},"CreatedAt":"2026-03-01T00:00:00Z","UpdatedAt":"2026-03-04T00:00:00Z","Spec":{"Name":"minecraft-server","Labels":{"gameserver.docker.managed":"true","gameserver.docker.Id":"minecraft-001","gameserver.docker.name":"Minecraft Survival","gameserver.docker.gametype":"minecraft","gameserver.docker.description":"Main world"},"TaskTemplate":{"ContainerSpec":{"Image":"minecraft:latest","Env":["EULA=true"]...
[WRN] 🔍 First service: ID=minecraft-service-id, Spec=True, SpecName=minecraft-server ✅
[DBG] Listed 2 services via agent

=== GAMESERVER DISCOVERY ===
[INF] Found 13 total services and 80 tasks
[INF] Converting services to GameServers in parallel...
[WRN] Service: minecraft-server, HasLabels: True, HasManagedLabel: True, ManagedValue: true ✅
[WRN] Service: valheim-server, HasLabels: True, HasManagedLabel: True, ManagedValue: true ✅
[WRN] Service: gameserver-docker, HasLabels: True, HasManagedLabel: False, ManagedValue: N/A
[INF] Found 2 GameServers out of 13 services ✅
```

---

## 🔍 Diagnostic Scenarios

### Scenario 1: Agent Returns Full Objects ✅

**Agent Log:**
```
[WRN] 📤 [Agent-ListServices] First service from Docker: Spec=True, SpecName=minecraft-server ✅
```

**Primary Log:**
```
[WRN] 🔍 First service: Spec=True, SpecName=minecraft-server ✅
```

**Conclusion:** Serialization working! ✅

---

### Scenario 2: Agent Returns Anonymous Objects ❌

**Agent Log:**
```
[WRN] 📤 [Agent-ListServices] First service from Docker: Spec=True, SpecName=minecraft-server ✅
[WRN] 📤 [Agent-ListServices] Sending response: {"Data":{"services":[{"ID":"abc","Name":"minecraft-server"}]}}
```

**Primary Log:**
```
[WRN] 📥 Raw JSON: {"Data":{"services":[{"ID":"abc","Name":"minecraft-server"}]}}
[WRN] 🔍 First service: Spec=False, SpecName=NULL ❌
```

**Conclusion:** Agent mapping to anonymous! Need fix 815d7dd!

---

### Scenario 3: Double Serialization Breaking ❌

**Agent Log:**
```
[WRN] 📤 Sending response: {"Data":{"services":[{"ID":"abc","Spec":{"Name":"minecraft-server"}...}]}} ✅
```

**Primary Log:**
```
[WRN] 📥 Raw JSON: {"Data":{"services":[{"ID":"abc","Spec":{"Name":"minecraft-server"}...}]}} ✅
[WRN] 🔍 First service: Spec=False, SpecName=NULL ❌
```

**Conclusion:** Primary deserialization broken! Need fix 55db78f!

---

### Scenario 4: JSON Structure Mismatch ❌

**Primary Log:**
```
[ERR] ❌ Response missing 'data.services' property
[ERR] ❌ Response JSON structure: {"success":true,"services":[...]}
```

**Conclusion:** Response structure doesn't match expected format!

---

## 🔧 What To Look For

### ✅ SUCCESS Indicators

**Agent sends full objects:**
```
📤 First service from Docker: Spec=True, SpecName=minecraft-server, Labels=5
📤 Sending response: {"Data":{"services":[{"ID":"...","Spec":{"Name":"minecraft-server"...
```

**Primary receives and parses:**
```
📥 Raw JSON from agent: {"Success":true...
📦 Services JSON: [{"ID":"...","Spec":{"Name":"minecraft-server"...
🔍 First service: ID=abc, Spec=True, SpecName=minecraft-server
```

**GameServer discovery works:**
```
Service: minecraft-server, HasLabels: True, ManagedLabel: True
Found 2 GameServers out of 13 services
```

---

### ❌ FAILURE Indicators

**Agent sends incomplete objects:**
```
📤 Sending response: {"Data":{"services":[{"ID":"abc"}]}}  ❌ Missing Spec!
```

**Primary can't deserialize:**
```
🔍 First service: ID=abc, Spec=False, SpecName=NULL  ❌
```

**GameServer discovery fails:**
```
Service: unknown, HasLabels: False  ❌
Found 0 GameServers  ❌
```

---

## 📋 Verification Checklist

After deploying 0.0.4.218:

### 1. Capability Filtering (Already Working in 0.0.4.217)
```bash
docker service logs gameserver-agent | grep "Agent registered:"
```
✅ Manager: `Capabilities=logs, exec, stats, attach, services`  
✅ Workers: `Capabilities=logs, exec, stats, attach`

---

### 2. Agent Sends Full Objects (NEW in 0.0.4.218)
```bash
docker service logs gameserver-agent | grep "📤 \[Agent-ListServices\]"
```
✅ `First service from Docker: Spec=True`  
✅ `Sending response: ...{"Spec":{"Name":`

---

### 3. Primary Receives Correctly (NEW in 0.0.4.218)
```bash
docker service logs gameserver-docker | grep "📥\|📦\|🔍"
```
✅ `📥 Raw JSON from agent: {"Success":true...`  
✅ `📦 Services JSON: [{"ID":"...","Spec":`  
✅ `🔍 First service: Spec=True, SpecName=minecraft-server`

---

### 4. GameServer Discovery Works
```bash
docker service logs gameserver-docker | grep "Service:\|Found.*GameServers"
```
✅ `Service: minecraft-server, HasLabels: True`  
✅ `Found 2 GameServers out of 13 services`

---

## 🚀 Deployment Commands

```bash
# Build with ALL fixes + comprehensive diagnostics
docker build -t gameserver-agent:0.0.4.218 -f src/GameServer.Docker.Agent/Dockerfile .
docker build -t gameserver-docker:0.0.4.218 -f src/GameServer.Docker/Dockerfile .

# Deploy
docker service update --image gameserver-agent:0.0.4.218 gameserver-agent
docker service update --image gameserver-docker:0.0.4.218 gameserver-docker

# Wait for complete rollout
watch docker service ps gameserver-docker gameserver-agent

# Verify version
docker service inspect gameserver-docker --format '{{.Spec.TaskTemplate.ContainerSpec.Image}}'
docker service inspect gameserver-agent --format '{{.Spec.TaskTemplate.ContainerSpec.Image}}'
```

---

## 📊 Log Analysis Commands

### Quick Health Check
```bash
# All emoji indicators at once
docker service logs gameserver-docker --since 5m | grep "📥\|📦\|🔍\|✅\|❌\|📤"
docker service logs gameserver-agent --since 5m | grep "📥\|📦\|🔍\|✅\|❌\|📤"
```

### Detailed Agent Communication
```bash
# See entire request/response cycle
docker service logs gameserver-docker --since 5m | grep -A 5 "Listing services via agent"
docker service logs gameserver-agent --since 5m | grep -A 5 "Listing services with filter"
```

### GameServer Discovery Status
```bash
# Final discovery results
docker service logs gameserver-docker --since 5m | grep "Found.*GameServers\|Service:.*HasLabels"
```

---

## 🐛 Debugging Different Failures

### If Agent Log Shows Spec=False
```
📤 First service from Docker: Spec=False, SpecName=NULL ❌
```
**Problem:** Docker returning services without Spec (shouldn't happen)  
**Action:** Check Docker daemon version, inspect service manually

---

### If Agent Log Shows Spec=True but Primary Shows Spec=False
```
Agent:   📤 Spec=True, SpecName=minecraft-server ✅
Primary: 🔍 Spec=False, SpecName=NULL ❌
```
**Problem:** Serialization breaking during transmission  
**Action:** Check "📤 Sending response" and "📥 Raw JSON" logs - compare structure

---

### If JSON Structure Doesn't Match
```
[ERR] ❌ Response missing 'data.services' property
[ERR] Response JSON structure: {"services":[...]}  ❌ Wrong structure!
```
**Problem:** Agent response format doesn't match expected structure  
**Action:** Agent might be using wrong response type

---

### If All Logs Look Good But Still Broken
```
Agent:   ✅ All good
Primary: ✅ All good  
But:     Service: unknown, HasLabels: False ❌
```
**Problem:** Issue in `DockerServiceHelper.GetAllGameServersAsync()` (not agent)  
**Action:** Check DockerServiceHelper logs, verify service conversion logic

---

## 📈 Expected Log Volume

**Per API call:** ~4-6 log lines  
**Per service list request:** ~20-30 lines (with 13 services)  
**Total:** Moderate (WARNING level, not DEBUG)

**Filter if too noisy:**
```bash
docker service logs gameserver-docker | grep -v "negotiate responded"
```

---

## 🎯 Success Pattern

**Complete successful flow:**

```
1. Primary requests services from agent
   [DBG] Listing services via agent on manager newdev-docker-001

2. Agent calls Docker
   [DBG] Listing services with filter: gameserver.docker.managed=true

3. Agent receives from Docker
   [WRN] 📤 First service from Docker: Spec=True, SpecName=minecraft-server ✅

4. Agent serializes and sends
   [WRN] 📤 Sending response: {"Success":true,"Data":{"services":[{"Spec":... ✅
   [INF] HTTP GET /api/services responded 200 in 45.2 ms

5. Primary receives raw JSON
   [WRN] 📥 Raw JSON from agent: {"Success":true,"Data":{"services":[{"Spec":... ✅

6. Primary extracts services array
   [WRN] 📦 Services JSON: [{"ID":"...","Spec":{"Name":"minecraft-server"... ✅

7. Primary deserializes to SwarmService
   [WRN] 🔍 First service: Spec=True, SpecName=minecraft-server ✅

8. GameServer discovery uses full objects
   [WRN] Service: minecraft-server, HasLabels: True, ManagedLabel: True ✅
   [INF] Found 2 GameServers out of 13 services ✅
```

**All checkmarks = working perfectly!** 🎉

---

## 🔍 Quick Diagnosis Script

Save as `diagnose-gameserver.sh`:

```bash
#!/bin/bash

echo "=== CHECKING AGENT CAPABILITIES ==="
docker service logs gameserver-agent --since 10m | grep "Agent registered:" | tail -6

echo ""
echo "=== CHECKING AGENT OUTPUT ==="
docker service logs gameserver-agent --since 10m | grep "📤 \[Agent-ListServices\]" | head -5

echo ""
echo "=== CHECKING PRIMARY INPUT ==="
docker service logs gameserver-docker --since 10m | grep "📥\|📦\|🔍" | head -10

echo ""
echo "=== CHECKING GAMESERVER DISCOVERY ==="
docker service logs gameserver-docker --since 10m | grep "Service:\|Found.*GameServers" | tail -15

echo ""
echo "=== SUMMARY ==="
docker service logs gameserver-docker --since 10m | grep "Found.*GameServers" | tail -1
```

Run with: `bash diagnose-gameserver.sh`

---

## 🎯 What Each Log Tells You

| Log Pattern | Meaning | Good | Bad |
|-------------|---------|------|-----|
| `📤 Spec=True` | Agent has full object | ✅ | `Spec=False` ❌ |
| `📤 Sending response: {"Data":{"services":[{"Spec":` | Agent sending full structure | ✅ | Missing `Spec` ❌ |
| `📥 Raw JSON: {"Success":true` | Primary received response | ✅ | `{"success":false` ❌ |
| `📦 Services JSON: [{"Spec":` | Services array has structure | ✅ | `[{"ID":` only ❌ |
| `🔍 Spec=True, SpecName=minecraft` | Deserialization worked | ✅ | `Spec=False` ❌ |
| `Service: minecraft-server` | Discovery using real name | ✅ | `Service: unknown` ❌ |
| `HasLabels: True` | Labels preserved | ✅ | `HasLabels: False` ❌ |
| `Found 2 GameServers` | Success! | ✅ | `Found 0 GameServers` ❌ |

---

## 🔒 Performance Impact

**Additional logging overhead:** Minimal (~5-10ms per call)  
**Benefit:** Complete visibility into serialization pipeline  
**When to disable:** After confirming everything works

**To disable later:** Change `LogWarning` to `LogTrace` in:
- `ServiceOperationsViaAgent.cs`
- `ServicesController.cs`

---

## ✅ Ready to Deploy!

**Build tag:** 0.0.4.218  
**Includes:**
- ✅ Capability filtering (59b8474)
- ✅ Agent full objects (815d7dd)
- ✅ Primary JsonDocument (55db78f)
- ✅ InspectService fix (f8106e2)
- ✅ Comprehensive diagnostics (7e579ed)
- ✅ 24 tests passing

**After deployment, logs will show EXACTLY what's happening at each step!** 🔍

---

**This is the most diagnostic version yet - nothing will be hidden!** 🎯
