# 🔍 Primary Service Connection Diagnostics

## Configuration Summary

### Agent Configuration (`gameserver-agent`)
```json
{
  "AgentRegistration": {
    "PrimaryServiceUrl": "http://gameserver-docker:8080",
    "HeartbeatIntervalSeconds": 30
  }
}
```

### Primary Service Configuration (`gameserver-docker`)
- **Hub Endpoint:** `/hubs/agentregistration` (registered in Program.cs:277)
- **Listening Port:** 8080 (from Dockerfile)
- **Full URL:** `http://gameserver-docker:8080/hubs/agentregistration`

---

## ✅ What's Correct

1. ✅ **Hub is registered** - `AgentRegistrationHub` mapped to `/hubs/agentregistration`
2. ✅ **Agent knows the URL** - Configured to connect to `http://gameserver-docker:8080`
3. ✅ **Port matches** - Both use port 8080
4. ✅ **Retry logic exists** - Agent will retry connection

---

## 🚨 Possible Issues & Solutions

### Issue 1: Primary Service Not Running

**Symptom:**
```
Connection refused (gameserver-docker:8080)
```

**Check:**
```bash
# List all services
docker service ls

# Check if gameserver-docker is running
docker service ps gameserver-docker

# Check container status
docker service ps gameserver-docker --format "{{.ID}}\t{{.Name}}\t{{.CurrentState}}"
```

**Solution:**
```bash
# Start the Primary Service
docker service scale gameserver-docker=1

# Or redeploy
docker stack deploy -c docker-stack.yml gameserver
```

---

### Issue 2: Service Name Not Resolvable

**Symptom:**
```
Could not resolve host: gameserver-docker
```

**Check:**
```bash
# Inside agent container
docker exec -it <agent-container-id> nslookup gameserver-docker
docker exec -it <agent-container-id> ping gameserver-docker
```

**Solution:**
Ensure both services are on the **same overlay network**:

```yaml
# docker-stack.yml
services:
  gameserver-docker:
    networks:
      - gameserver-net
  
  gameserver-agent:
    networks:
      - gameserver-net

networks:
  gameserver-net:
    driver: overlay
```

---

### Issue 3: Different Overlay Networks

**Symptom:**
```
Connection refused or timeout
```

**Check:**
```bash
# List networks
docker network ls

# Inspect service networks
docker service inspect gameserver-docker --format '{{json .Spec.TaskTemplate.Networks}}'
docker service inspect gameserver-agent --format '{{json .Spec.TaskTemplate.Networks}}'
```

**Solution:**
Both services **must share** an overlay network.

---

### Issue 4: Port Not Exposed

**Check:**
```bash
# Check if port 8080 is published
docker service inspect gameserver-docker --format '{{json .Endpoint.Ports}}'
```

**Note:** For **internal** communication, ports don't need to be published - services can reach each other via overlay network on any exposed port.

---

### Issue 5: Primary Service Started After Agents

**Symptom:**
```
Failed to connect (attempt 1/30). Retrying in 5s...
```

**This is NORMAL!** The retry logic we added handles this:
- Agents retry for ~5-10 minutes
- Once Primary Service starts, agents connect automatically

**No action needed** - just wait for Primary Service to start.

---

## 🧪 Diagnostic Commands

### 1. Check Service Logs

```bash
# Primary Service logs
docker service logs gameserver-docker --tail 50 --follow

# Look for:
[INFO] Now listening on: http://0.0.0.0:8080
[INFO] Application started
```

### 2. Check Agent Logs

```bash
# Agent logs
docker service logs gameserver-agent --tail 50 --follow

# Look for:
[INFO] Agent Registration Service starting
[DEBUG] Primary Service connectivity check: Host=gameserver-docker, Port=8080
[WRN] Failed to connect (attempt X/30). Retrying in Ys...
```

### 3. Test Connectivity

```bash
# From agent container
docker exec -it <agent-container> sh

# Test DNS
nslookup gameserver-docker
# Should return IP like: 10.0.1.x

# Test port
nc -zv gameserver-docker 8080
# or
telnet gameserver-docker 8080

# Test HTTP
wget -O- http://gameserver-docker:8080/health
# or
curl http://gameserver-docker:8080/health
```

### 4. Check Network Connectivity

```bash
# Inspect overlay network
docker network inspect <network-name>

# Check which containers are connected
docker network inspect <network-name> --format '{{range .Containers}}{{.Name}} {{.IPv4Address}}{{"\n"}}{{end}}'
```

---

## 🔧 Enhanced Logging (Added)

The agent now logs these diagnostics:

```
[DEBUG] Primary Service connectivity check: Host=gameserver-docker, Port=8080, Scheme=http
[DEBUG] Agent environment: Hostname=gameserver-agent.1.xyz, Machine=gameserver-agent.1.xyz
[DEBUG] Agent network identity: 
        DockerNodeHostname=newdev-docker-004
        TaskHostname=gameserver-agent.1.xyz123
        ServiceUrl=http://gameserver-agent.1.xyz123:8080
```

---

## 📋 Troubleshooting Checklist

- [ ] Primary Service (`gameserver-docker`) is running
- [ ] Primary Service is listening on port 8080
- [ ] Agent can resolve `gameserver-docker` hostname
- [ ] Both services are on the same overlay network
- [ ] No firewall blocking port 8080
- [ ] Hub is registered at `/hubs/agentregistration`
- [ ] Agent has correct `PrimaryServiceUrl` configuration

---

## 🎯 Most Likely Cause

Based on your logs showing **"Connection refused"**, the most likely cause is:

**⚠️ Primary Service hasn't started yet**

The retry logic will handle this automatically. Check if `gameserver-docker` service is running:

```bash
docker service ls | grep gameserver-docker
```

If it's not running or starting, check:

```bash
docker service ps gameserver-docker --no-trunc
docker service logs gameserver-docker
```

---

## 🚀 Quick Fix Commands

```bash
# 1. Check services status
docker service ls

# 2. Check Primary Service
docker service ps gameserver-docker

# 3. If not running, scale it up
docker service scale gameserver-docker=1

# 4. Watch logs
docker service logs -f gameserver-docker

# 5. In another terminal, watch agent logs
docker service logs -f gameserver-agent

# 6. Wait for connection
# You should see agents connect within 30 seconds after Primary starts
```

---

## 💡 Expected Behavior

1. **Agent starts** → Tries to connect → Primary not ready → **Retries**
2. **Primary starts** → Listens on port 8080
3. **Agent retries** → Connects successfully → Registers

**Timeline:**
```
T+0s:   Agent starts, tries connection → fails
T+5s:   Retry 1 → fails
T+12s:  Retry 2 → fails
T+23s:  Retry 3 → fails
...
T+60s:  Primary Service starts
T+65s:  Retry X → SUCCESS! ✅
```

This is **working as designed** with the retry logic!

---

**Next Step:** Check if Primary Service is running, and share the output of `docker service ps gameserver-docker`
