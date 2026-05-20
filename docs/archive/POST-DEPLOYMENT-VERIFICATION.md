# Post-Deployment Verification Guide

**Version:** 0.0.4.216+  
**Fixes:** Capability filtering + Agent/Primary serialization bugs  
**Tests:** 24 new tests (all passing)

---

## 🎯 Quick Verification

### 1. Check Agent Capabilities

```bash
docker service logs gameserver-agent | grep "Agent registered:" | tail -6
```

**Expected Output:**
```
Manager Node:
[INF] Agent registered: Capabilities=logs, exec, stats, attach, services, Manager=True ✅

Worker Nodes:
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
```

**✅ SUCCESS:** Manager has "services", workers don't  
**❌ FAILURE:** All nodes show same capabilities

---

### 2. Check Service Discovery

```bash
docker service logs gameserver-docker | grep "Service:" | tail -10
```

**Expected Output:**
```
[WRN] Service: gameserver-docker, HasLabels: True, HasManagedLabel: False, ManagedValue: N/A
[WRN] Service: gameserver-agent, HasLabels: True, HasManagedLabel: False, ManagedValue: N/A
[WRN] Service: postgres, HasLabels: False, HasManagedLabel: False, ManagedValue: N/A
[WRN] Service: redis, HasLabels: True, HasManagedLabel: False, ManagedValue: N/A
[WRN] Service: minecraft-server, HasLabels: True, HasManagedLabel: True, ManagedValue: true ✅
[WRN] Service: valheim-server, HasLabels: True, HasManagedLabel: True, ManagedValue: true ✅
```

**✅ SUCCESS:** Real service names, correct label detection  
**❌ FAILURE:** All showing "Service: unknown, HasLabels: False"

---

### 3. Check GameServer Count

```bash
docker service logs gameserver-docker | grep "Found.*GameServers" | tail -1
```

**Expected Output:**
```
[INF] Found 2 GameServers out of 13 services
```

**✅ SUCCESS:** Count > 0 (if you have services with `gameserver.docker.managed=true` label)  
**❌ FAILURE:** Still showing 0 GameServers

---

### 4. Check Dashboard

```bash
curl http://gameserver-docker:8080/api/servers | jq
```

**Expected Output:**
```json
[
  {
    "id": "minecraft-001",
    "name": "Minecraft Survival Server",
    "gameType": "minecraft",
    "status": "running",
    ...
  },
  {
    "id": "valheim-001",
    "name": "Valheim Server",
    "gameType": "valheim",
    "status": "running",
    ...
  }
]
```

**✅ SUCCESS:** GameServers listed  
**❌ FAILURE:** Empty array `[]`

---

## 🔧 Troubleshooting

### Issue: Still Showing "Service: unknown"

**Diagnosis:**
```bash
# Check which version is running
docker service inspect gameserver-docker --format '{{.Spec.TaskTemplate.ContainerSpec.Image}}'
docker service inspect gameserver-agent --format '{{.Spec.TaskTemplate.ContainerSpec.Image}}'
```

**Fix:** Ensure both services updated to 0.0.4.216+

---

### Issue: Services Found But 0 GameServers

**Diagnosis:**
```bash
# Check if services have the required label
docker service inspect minecraft-server --format '{{json .Spec.Labels}}' | jq
```

**Expected:**
```json
{
  "gameserver.docker.managed": "true",
  "gameserver.docker.Id": "minecraft-001",
  "gameserver.docker.name": "Minecraft Server",
  "gameserver.docker.gametype": "minecraft"
}
```

**Fix:** Add labels to your GameServer services:

```bash
docker service update \
  --label-add gameserver.docker.managed=true \
  --label-add gameserver.docker.Id=$(uuidgen) \
  --label-add gameserver.docker.name="Minecraft Server" \
  --label-add gameserver.docker.gametype=minecraft \
  minecraft-server
```

---

### Issue: Worker Nodes Still Show "services" Capability

**Diagnosis:**
```bash
# Check agent version
docker service ps gameserver-agent --filter "desired-state=running" --format "{{.Image}}"
```

**Fix:** Force update to ensure new version:

```bash
docker service update --force --image gameserver-agent:0.0.4.216 gameserver-agent
```

---

## 🧪 Test Verification

### Run Tests Locally

```bash
# All new tests
dotnet test --filter "FullyQualifiedName~ServiceOperationsViaAgent|FullyQualifiedName~DockerModelSerialization|FullyQualifiedName~ServicesController|FullyQualifiedName~CapabilityFiltering"
```

**Expected:** 24/24 passing

---

## 📊 Health Checks

### Agent Health

```bash
curl http://manager-agent:8080/health
curl http://worker-agent-1:8080/health
```

**Expected:** `{"status":"Healthy"}`

### Primary Service Health

```bash
curl http://gameserver-docker:8080/health
```

**Expected:** `{"status":"Healthy"}`

### Agent Registry

```bash
curl http://gameserver-docker:8080/api/debug/agents
```

**Expected:**
```json
[
  {
    "nodeName": "newdev-docker-001",
    "isManagerNode": true,
    "isHealthy": true,
    "capabilities": ["logs", "exec", "stats", "attach", "services"]
  },
  {
    "nodeName": "newdev-docker-002",
    "isManagerNode": false,
    "isHealthy": true,
    "capabilities": ["logs", "exec", "stats", "attach"]
  }
]
```

---

## 🎯 Success Criteria

### ✅ All Must Pass

- [ ] Manager agent shows "services" capability
- [ ] Worker agents don't show "services" capability
- [ ] Service logs show real names (not "unknown")
- [ ] Service logs show `HasLabels: True` for labeled services
- [ ] GameServer count > 0 (if labels exist)
- [ ] Dashboard shows GameServers
- [ ] Create new GameServer works
- [ ] 24 tests passing

---

## 🚨 If Issues Persist

### Get Detailed Logs

```bash
# Get full Primary Service logs
docker service logs gameserver-docker --no-trunc > primary-logs.txt

# Get full Agent logs
docker service logs gameserver-agent --no-trunc > agent-logs.txt

# Check for errors
grep -i "error\|exception\|failed" primary-logs.txt agent-logs.txt
```

### Check Docker Swarm State

```bash
# List all nodes
docker node ls

# Check which nodes are managers
docker node ls --filter "role=manager"

# Inspect a specific node
docker node inspect newdev-docker-001 --format '{{.Spec.Role}}'
```

### Verify Network Connectivity

```bash
# From Primary Service container
docker exec $(docker ps --filter "label=com.docker.swarm.service.name=gameserver-docker_gameserver-docker" -q | head -1) \
  wget -O- http://gameserver-agent:8080/health

# From Agent container to Primary
docker exec $(docker ps --filter "label=com.docker.swarm.service.name=gameserver-agent" -q | head -1) \
  wget -O- http://gameserver-docker_gameserver-docker:8080/health
```

---

## 📞 Support

### Diagnostic Endpoints

```bash
# Agent info
GET http://manager-agent:8080/api/info

# Primary agent registry
GET http://gameserver-docker:8080/api/debug/agents

# Service list via agent
GET http://manager-agent:8080/api/services
```

---

## 🎉 Expected Success Scenario

1. **Deploy** new images (0.0.4.216+)
2. **Wait** for rollout (~30 seconds)
3. **Check logs** - See capability filtering working
4. **Refresh dashboard** - GameServers appear!
5. **Create test server** - Works via agent routing
6. **Monitor** - Everything works smoothly

---

## 📝 Rollback Plan (If Needed)

```bash
# Get previous version
docker service inspect gameserver-docker --format '{{.PreviousSpec.TaskTemplate.ContainerSpec.Image}}'

# Rollback
docker service update --rollback gameserver-docker
docker service update --rollback gameserver-agent
```

**Note:** Previous versions have the bugs, but GameServers will still work if you add labels manually

---

## 🎓 What Was Fixed

### The Triple Bug

1. **Capability Filtering** → Agents advertise correct capabilities
2. **Agent Mapping** → Agent returns full objects
3. **Primary Deserialization** → Primary deserializes correctly

**All three had to be fixed!** One without the others wouldn't work.

---

**You're ready to deploy!** 🚀

After deployment, your dashboard should finally show your managed GameServers!
