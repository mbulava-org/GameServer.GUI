# Agent-Based Architecture Testing Guide

## Overview

This guide covers testing the new agent-based architecture where:
- **Agents** connect to Primary Service and register themselves
- **Service operations** are delegated to manager node agents
- **Primary Service** can run without direct Docker connection

## Test Phases

### Phase 1: Agent Registration Testing

#### Test 1.1: Agent Startup and Registration

**Objective:** Verify agents successfully register on startup

**Steps:**
1. Start GameServer.Docker (Primary Service)
2. Start GameServer.Docker.Agent on each node
3. Check Primary Service logs for registration messages

**Expected Output:**
```
[INFO] Agent registered: Node=worker-1 (abc123def), ConnectionId=xyz, Url=http://172.18.0.5:8080, Manager=False
[INFO] Agent registered: Node=manager-1 (ghi456jkl), ConnectionId=uvw, Url=http://172.18.0.3:8080, Manager=True
```

**Verification:**
- Each agent logs: `Agent registered with Primary Service`
- Primary logs show all agents with correct `IsManager` status
- At least one manager node present

#### Test 1.2: Agent Heartbeats

**Objective:** Verify agents send periodic heartbeats

**Steps:**
1. Wait 30+ seconds after agent registration
2. Check Primary Service logs for heartbeat messages

**Expected Output:**
```
[TRACE] Agent heartbeat: Node=worker-1, Containers=3 [abc123, def456, ghi789]
```

**Verification:**
- Heartbeats arrive every 30 seconds (configurable)
- Container counts match actual running containers
- No missed heartbeats or connection drops

#### Test 1.3: Agent Reconnection

**Objective:** Verify agents automatically reconnect

**Steps:**
1. Restart Primary Service while agents are running
2. Wait for agents to reconnect
3. Check logs on both sides

**Expected Output (Agent):**
```
[WARN] Lost connection to Primary Service, reconnecting...
[INFO] Reconnected to Primary Service with ConnectionId=new-xyz
[INFO] Agent registered with Primary Service: Node=worker-1
```

**Expected Output (Primary):**
```
[INFO] Agent connected: ConnectionId=new-xyz
[INFO] Agent registered: Node=worker-1 (abc123def)
```

---

### Phase 2: Container Lookup Testing

#### Test 2.1: Registry-Based Lookup

**Objective:** Verify container lookups use registry first

**Steps:**
1. Create a game server
2. Check Primary Service logs when accessing server (logs, console, etc.)

**Expected Output:**
```
✅ Found agent via REGISTRY (push-based) for container abc123: http://172.18.0.5:8080 on node worker-1
```

**Verification:**
- No Docker API queries for agent discovery
- Lookup completes in <1ms (in-memory dictionary)
- Fallback message NOT present

#### Test 2.2: Fallback to Discovery

**Objective:** Verify fallback when registry doesn't have container

**Steps:**
1. Disable agent registration: `AgentRegistration:Enabled=false`
2. Start agent (old discovery method only)
3. Create a game server and access it

**Expected Output:**
```
⚠️ Agent not found in registry for container abc123, falling back to Docker Swarm query
✅ Found agent via DISCOVERY (pull-based) for container abc123: http://172.18.0.5:8080 on node worker-1
```

**Verification:**
- System still works with discovery as fallback
- Discovery queries Docker API (slower, but functional)

---

### Phase 3: Service Operations via Agent

#### Test 3.1: Service Creation via Agent (Mode=Agent)

**Objective:** Verify service creation works through agent

**Configuration:**
```json
{
  "ServiceOperations": {
    "Mode": "Agent",
    "Enabled": true
  }
}
```

**Steps:**
1. Set `ServiceOperations:Mode=Agent` in Primary Service
2. Restart Primary Service
3. Create a new game server

**Expected Output (Primary):**
```
🔄 Service operations mode: AGENT (via manager node agent)
[INFO] Creating service via agent: minecraft-server on manager manager-1
[INFO] Service created successfully: service-id-xyz
```

**Expected Output (Agent):**
```
[INFO] Creating service: minecraft-server
[INFO] Service created successfully: service-id-xyz
```

**Verification:**
- Game server starts successfully
- Service visible in Docker Swarm
- No direct Docker connection from Primary Service

#### Test 3.2: Service Update via Agent

**Objective:** Verify service updates work through agent

**Steps:**
1. Update game server settings (e.g., change environment variable)
2. Save changes
3. Check logs

**Expected Output:**
```
[INFO] Updating service via agent: service-id-xyz on manager manager-1
[INFO] Service updated successfully: service-id-xyz
```

**Verification:**
- Service updates in Docker Swarm
- New settings take effect
- No errors in logs

#### Test 3.3: Service Deletion via Agent

**Objective:** Verify service deletion works through agent

**Steps:**
1. Delete a game server
2. Check logs and Docker Swarm

**Expected Output:**
```
[INFO] Deleting service via agent: service-id-xyz on manager manager-1
[INFO] Service deleted successfully: service-id-xyz
```

**Verification:**
- Service removed from Docker Swarm
- Container stops running
- Database updated

---

### Phase 4: Service Operations Direct Mode

#### Test 4.1: Service Creation via Direct Connection (Mode=Direct)

**Objective:** Verify backward compatibility with direct mode

**Configuration:**
```json
{
  "ServiceOperations": {
    "Mode": "Direct",
    "Enabled": true
  }
}
```

**Steps:**
1. Set `ServiceOperations:Mode=Direct`
2. Restart Primary Service
3. Create a game server

**Expected Output:**
```
🔄 Service operations mode: DIRECT (via Docker client)
[INFO] Creating service via direct Docker connection: minecraft-server
```

**Verification:**
- Game server starts successfully
- Direct Docker API calls used
- System behavior unchanged from before

---

### Phase 5: End-to-End Testing

#### Test 5.1: Complete Server Lifecycle (Agent Mode)

**Objective:** Verify full server lifecycle in agent mode

**Steps:**
1. **Create** game server
2. **Start** server (if not auto-started)
3. **View logs** via web UI
4. **Open console** and run commands
5. **Monitor resources** (CPU, memory)
6. **Update** server settings
7. **Stop** server
8. **Delete** server

**Verification at Each Step:**
- ✅ No errors in Primary Service logs
- ✅ No errors in Agent logs
- ✅ Operations complete successfully
- ✅ Web UI updates correctly
- ✅ No direct Docker API calls from Primary (when Mode=Agent)

#### Test 5.2: Multi-Node Deployment

**Objective:** Verify system works across multiple nodes

**Setup:**
- 1 Manager node
- 2+ Worker nodes
- Agents on all nodes
- Primary Service connected to agents

**Steps:**
1. Create multiple game servers
2. Verify servers distribute across nodes
3. Access each server (logs, console, stats)
4. Check container-to-agent mappings

**Verification:**
- All agents register successfully
- Servers distribute evenly (depending on placement constraints)
- Container operations route to correct agent
- No cross-node confusion

---

### Phase 6: Failure Testing

#### Test 6.1: Agent Disconnection

**Objective:** Test system behavior when agent disconnects

**Steps:**
1. Create game server on worker-1
2. Stop agent on worker-1
3. Try to access server logs/console

**Expected Behavior:**
- Agent marked as disconnected in Primary
- Container operations fail gracefully
- Error message indicates agent unavailable

**Recovery:**
1. Restart agent on worker-1
2. Agent re-registers automatically
3. Container operations work again

#### Test 6.2: Manager Agent Unavailable

**Objective:** Test service operations when no manager agent available

**Steps:**
1. Set Mode=Agent
2. Stop all manager node agents
3. Try to create a game server

**Expected Output:**
```
[ERROR] No healthy manager agent available for service operations.
Total agents: 2, Manager agents: 0, Healthy managers: 0
```

**Verification:**
- Service creation fails with clear error
- System doesn't crash
- Other operations (container ops) still work

#### Test 6.3: Primary Service Restart

**Objective:** Verify agents reconnect after Primary restarts

**Steps:**
1. Create several game servers
2. Restart Primary Service
3. Wait for agents to reconnect

**Expected Behavior:**
- Agents reconnect within 30 seconds
- All agents re-register
- Container mappings restored via heartbeats
- No data loss
- Servers remain running

---

### Phase 7: Performance Testing

#### Test 7.1: Agent Lookup Performance

**Objective:** Compare registry vs discovery performance

**Method:**
1. Create 10 game servers
2. Access each server 100 times (logs/stats)
3. Measure time for agent lookups

**Expected Results:**
- **Registry lookup:** <1ms average
- **Discovery lookup:** 100-500ms average
- **Performance gain:** 100-500x faster

#### Test 7.2: Service Operation Performance

**Objective:** Compare Agent mode vs Direct mode

**Method:**
1. Create 10 services in Agent mode (measure time)
2. Create 10 services in Direct mode (measure time)

**Expected Results:**
- Agent mode: Slightly slower (+10-50ms overhead)
- Agent mode: No persistent Docker connection needed
- Trade-off: Acceptable for architecture benefits

#### Test 7.3: Scale Testing

**Objective:** Test with many servers and agents

**Setup:**
- 3+ manager nodes
- 10+ worker nodes
- 50+ game servers

**Metrics to Monitor:**
- Agent registration time
- Heartbeat processing load
- Container lookup performance
- Service operation success rate

**Acceptance Criteria:**
- All agents register within 60 seconds
- Heartbeat lag <5 seconds
- Container lookup <10ms 95th percentile
- Service operations >99% success rate

---

## Configuration Testing

### Test: Mode Switching

**Objective:** Verify seamless switching between modes

**Steps:**
1. Start in Direct mode
2. Create servers, verify functionality
3. Stop Primary Service
4. Change to Agent mode
5. Start Primary Service
6. Verify existing servers still work
7. Create new servers

**Verification:**
- Existing servers unaffected
- New operations use agent mode
- No data migration needed
- Configuration change is non-disruptive

---

## Regression Testing

### Critical Paths to Test

After any changes to agent architecture:

1. **Server Creation**
   - ✅ Via API
   - ✅ Via Web UI
   - ✅ With all game types

2. **Server Operations**
   - ✅ Start/Stop
   - ✅ Update settings
   - ✅ Delete
   - ✅ View logs
   - ✅ Interactive console

3. **Resource Monitoring**
   - ✅ Real-time stats
   - ✅ CPU/Memory graphs
   - ✅ Network I/O

4. **Multi-Server Scenarios**
   - ✅ Create 10+ servers
   - ✅ All servers accessible
   - ✅ No resource leaks

---

## Troubleshooting Test Failures

### Agent Won't Register

**Symptoms:**
- Agent logs show connection errors
- Primary doesn't receive registration

**Check:**
1. Network connectivity: `curl http://primary:8080/hubs/agentregistration`
2. Agent configuration: `AgentRegistration:PrimaryServiceUrl`
3. Firewall rules
4. SignalR endpoint mapped in Primary

### Service Operations Fail in Agent Mode

**Symptoms:**
- "No healthy manager agent available"

**Check:**
1. At least one manager node has agent running
2. Agent logs show `IsManager=True`
3. Manager agent is healthy (recent heartbeat)
4. Manager agent `/api/services` endpoint responds

### Container Operations Fail

**Symptoms:**
- Logs/console don't load
- "No agent found for container"

**Check:**
1. Agent running on node where container is
2. Container ID in heartbeat messages
3. Agent registry has mapping: Primary logs show heartbeat processing
4. Container actually running: `docker ps` on node

---

## Automated Testing Checklist

### Unit Tests

- [ ] AgentRegistryService.RegisterAgent()
- [ ] AgentRegistryService.GetAgentForContainer()
- [ ] AgentRegistryService.GetHealthyManagerAgent()
- [ ] ServiceOperationsViaAgent (mocked HTTP)
- [ ] ServiceOperationsViaDirect (mocked Docker client)

### Integration Tests

- [ ] Agent registration flow
- [ ] Heartbeat processing
- [ ] Container-to-agent mapping
- [ ] Service creation via agent
- [ ] Mode switching (Direct ↔ Agent)

### End-to-End Tests

- [ ] Full server lifecycle
- [ ] Multi-node deployment
- [ ] Agent reconnection
- [ ] Primary Service restart

---

## Success Criteria

### Phase 1-3 (Current Status)

- ✅ Agents register on startup
- ✅ Heartbeats every 30 seconds
- ✅ Container lookups via registry
- ✅ Fallback to discovery works

### Phase 4-5 (Service Operations)

- ✅ Service operations via agent work
- ✅ Direct mode still works (backward compatible)
- ✅ Primary runs without Docker in Agent mode
- ✅ All Docker operations abstracted

### Production Readiness

- [ ] All test phases pass
- [ ] Performance meets requirements
- [ ] Documentation complete
- [ ] Migration guide tested
- [ ] Rollback procedure verified
- [ ] Monitoring/alerting configured
