# Manual Multi-Node Docker Swarm Test Plan

This document lists the manual tests needed to confirm that the multi-node Docker Swarm + Node Agent architecture works end-to-end. Use it before declaring the feature battle-tested.

## Prerequisites

- A Docker Swarm with at least two nodes (one manager, one worker).
- Both nodes can reach each other and the Primary Service on the overlay network.
- The agent image is built (`gameserver-agent:latest`) and pushed to a registry if testing on remote nodes.

## Setup checklist

- [ ] `docker network create --driver overlay --attachable gameserver-network` created on the manager.
- [ ] Primary Service stack deployed on `gameserver-network`.
- [ ] Agent stack deployed as a **global** service on `gameserver-network`.
- [ ] Agent `appsettings.json` or environment variable sets `AgentRegistration__PrimaryServiceUrl` to the Primary Service's in-network URL.
- [ ] At least one agent registers with `IsManagerNode = true` and capabilities include `services`, `tasks`, `nodes`, `swarm`.

## Test 1: Agent registration and discovery

| # | Step | Expected result |
|---|------|-----------------|
| 1.1 | On each node, run `docker ps` and verify an agent container is running. | One agent container per node. |
| 1.2 | Check agent logs for `Agent registered with Primary Service`. | Registration succeeded on every node. |
| 1.3 | Open the Blazor UI and navigate to the agent/node list (if available) or check the server detail page for agent health. | All nodes show healthy agents. |
| 1.4 | Stop one agent container (`docker stop <agent-id>`). | Primary Service marks that agent unhealthy/unavailable after missed heartbeats. |
| 1.5 | Restart the agent container. | Agent reconnects and registers again, agent becomes healthy. |
| 1.6 | Drain one Swarm node (`docker node update --availability drain <node>`). | Agent on that node is evicted/removed from the agent registry. |
| 1.7 | Reactivate the node (`docker node update --availability active <node>`). | New agent task schedules and registers. |

## Test 2: Manager capability filtering

| # | Step | Expected result |
|---|------|-----------------|
| 2.1 | On the manager node, inspect agent logs. | Capabilities include `services`, `tasks`, `nodes`, `swarm`. |
| 2.2 | On a worker node, inspect agent logs. | Capabilities do NOT include `services`, `tasks`, `nodes`, `swarm`. |
| 2.3 | Simulate no manager agent by scaling the manager node agent to 0 or draining the manager. | Creating a server returns an error such as `No healthy manager agent available`. |

## Test 3: Multi-node service deployment

| # | Step | Expected result |
|---|------|-----------------|
| 3.1 | Create a V2 game server through the Blazor UI or API. | Server record is created in the database. |
| 3.2 | Start the server. | Primary Service delegates service creation to a manager agent. |
| 3.3 | On the manager node, run `docker service ls`. | A new Swarm service exists for the server. |
| 3.4 | On the manager node, run `docker service ps <service-name>`. | One or more tasks are running on Swarm nodes. |
| 3.5 | Verify the task landed on a worker node. | A worker node hosts the running container. |

## Test 4: Container operations across nodes

| # | Step | Expected result |
|---|------|-----------------|
| 4.1 | In the UI, open the server that is running on a worker node. | Server detail loads (no direct Docker daemon access required). |
| 4.2 | Open the **Logs** tab. | Logs stream from the container through the worker agent. |
| 4.3 | Open the **Terminal** tab and run a command. | Command executes on the worker node through the worker agent. |
| 4.4 | Open the **Console** tab. | Console attaches to the container on the worker node. |
| 4.5 | View the **Resource Monitor** / stats. | CPU/memory/network stats stream from the worker agent. |

## Test 5: Server update and reschedule

| # | Step | Expected result |
|---|------|-----------------|
| 5.1 | Edit an existing server (e.g. change an environment variable setting). | Update call succeeds. |
| 5.2 | Verify the Swarm service was updated. | `docker service inspect <service-name>` shows the new env vars or image. |
| 5.3 | Stop the server. | Service is removed from Swarm. |
| 5.4 | Start the server again. | Service is recreated and a new task starts. |

## Test 6: Overlay network and placement constraints

| # | Step | Expected result |
|---|------|-----------------|
| 6.1 | Add a node label to a worker (`docker node update --label-add size=small <node>`). | Label is visible via `docker node inspect <node>`. |
| 6.2 | Configure a game type/revision volume or placement constraint referencing the label. | Constraint is included in the service create/update request. |
| 6.3 | Create/start a server using that revision. | Service lands on the labelled node (or fails placement with a clear error). |
| 6.4 | Confirm agent on the target node can reach the Primary Service and vice versa. | No `connection refused` or timeout errors in logs. |

## Test 7: Failure and recovery

| # | Step | Expected result |
|---|------|-----------------|
| 7.1 | While a server is running, stop the worker agent that hosts its container. | Log/terminal streams fail gracefully; UI shows an error or reconnects. |
| 7.2 | Restart the worker agent. | Agent re-registers; container operations resume. |
| 7.3 | Stop all manager agents. | Service create/update/delete operations fail fast with a clear manager-agent error. |
| 7.4 | Restore a manager agent. | Service operations succeed again. |

## Test 8: Port allocation across nodes

| # | Step | Expected result |
|---|------|-----------------|
| 8.1 | Create multiple servers on different nodes. | Each server gets a unique published port. |
| 8.2 | Verify no published port collisions occur. | `docker service inspect <service>` shows unique host ports. |
| 8.3 | Delete a server. | Port is released and can be reused by a new server. |

## Known gaps to watch for

- If `AgentRegistration__PrimaryServiceUrl` points to a published host port instead of the in-network service name, agents may fail when the manager changes or if the host is unreachable from inside containers.
- `EnableBackgroundDiscovery` in `NodeAgentOptions` is deprecated. If it is enabled, it performs no Swarm polling; agents should register via SignalR.
- Live streams (logs, terminal, stats) rely on the correct container-to-agent mapping. If an agent is evicted before the stream starts, the UI should retry or show an error.

## Sign-off

When all tests pass, update `docs/CURRENT-FEATURES.md` to move "Multi-node Docker Swarm support" from **Implemented but not battle-tested** to **Core Features**.
