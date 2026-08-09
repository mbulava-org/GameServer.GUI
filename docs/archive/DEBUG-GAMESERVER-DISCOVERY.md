# 🔍 Debugging GameServer Discovery

## 🚨 Problem

GameServers exist in Docker Swarm but aren't being discovered by the dashboard.

**Logs show:**
```
[INF] Found 13 total services and 80 tasks
[INF] Converting services to GameServers in parallel...
[INF] Found 0 GameServers out of 13 services  ← Problem!
```

---

## 🎯 How GameServer Discovery Works

### Required Service Labels

For a Docker Swarm service to be recognized as a GameServer, it **MUST** have these labels:

```yaml
services:
  my-minecraft-server:
    image: minecraft:latest
    deploy:
      labels:
        gameserver.docker.managed: "true"        # ← REQUIRED!
        gameserver.docker.Id: "unique-id-123"    # ← Unique ID
        gameserver.docker.name: "My Server"      # ← Display name
        gameserver.docker.gametype: "minecraft"  # ← Game type
        gameserver.docker.description: "..."     # ← Optional
```

**Key Points:**
- Labels go in `deploy.labels` (not container labels!)
- `gameserver.docker.managed` **must** equal `"true"` (string, not boolean)
- All label keys are defined in `ServiceLabels` constants

---

## 🧪 Diagnostic Steps

### Step 1: Check Existing Services

```bash
# List all services
docker service ls

# Inspect a specific service
docker service inspect <service-name> --pretty

# Check labels on a service
docker service inspect <service-name> --format '{{json .Spec.Labels}}' | jq
```

### Step 2: Look for GameServer Labels

```bash
# Find services with the managed label
docker service ls --filter "label=gameserver.docker.managed=true"

# This should show your GameServers!
```

**Expected output:**
```
ID            NAME               MODE        REPLICAS   IMAGE
abc123def456  minecraft-server   replicated  1/1        minecraft:latest
xyz789ghi012  valheim-server     replicated  1/1        valheim:latest
```

**If empty:** Your services don't have the required labels!

---

## 🔧 How to Add Labels to Existing Services

### Option 1: Update Service (Recommended)

```bash
docker service update \
  --label-add gameserver.docker.managed=true \
  --label-add gameserver.docker.Id=unique-server-id \
  --label-add gameserver.docker.name="My Minecraft Server" \
  --label-add gameserver.docker.gametype=minecraft \
  <service-name>
```

### Option 2: Redeploy with Labels

```yaml
# docker-stack.yml
services:
  minecraft-server:
    image: minecraft:latest
    deploy:
      labels:
        gameserver.docker.managed: "true"
        gameserver.docker.Id: "minecraft-001"
        gameserver.docker.name: "Survival Server"
        gameserver.docker.gametype: "minecraft"
        gameserver.docker.description: "Main survival world"
```

Then redeploy:
```bash
docker stack deploy -c docker-stack.yml gameserver
```

---

## 🔍 Enhanced Logging (Added)

The system now logs diagnostic info when no GameServers are found:

```
[WRN] No GameServers found among 13 services. Checking labels...
[DBG] Service: minecraft-server, HasLabels: True, HasManagedLabel: True, ManagedValue: true ✅
[DBG] Service: my-app, HasLabels: True, HasManagedLabel: False, ManagedValue: N/A ❌
[DBG] Service: postgres, HasLabels: False, HasManagedLabel: False, ManagedValue: N/A ❌
```

**Look for:**
- `HasLabels: False` → Service has no labels at all
- `HasManagedLabel: False` → Service missing `gameserver.docker.managed` label
- `ManagedValue: N/A` → Label not present
- `ManagedValue: false` → Wrong value (must be `"true"` string)

---

## ⚠️ Common Issues

### Issue 1: Labels on Container Instead of Service

**Wrong:**
```yaml
services:
  minecraft:
    image: minecraft
    labels:  # ❌ These are container labels!
      gameserver.docker.managed: "true"
```

**Correct:**
```yaml
services:
  minecraft:
    image: minecraft
    deploy:
      labels:  # ✅ These are service labels!
        gameserver.docker.managed: "true"
```

---

### Issue 2: Wrong Label Value Type

**Wrong:**
```yaml
gameserver.docker.managed: true  # ❌ Boolean
```

**Correct:**
```yaml
gameserver.docker.managed: "true"  # ✅ String
```

---

### Issue 3: Labels Not Applied Yet

After updating labels, the service needs to redeploy:

```bash
# Force update to apply labels
docker service update --force <service-name>

# Or scale down/up
docker service scale <service-name>=0
docker service scale <service-name>=1
```

---

### Issue 4: Using Old GameServer Creation API

If you created GameServers via the API, check the code adds labels:

```csharp
// In CreateGameServerAsync or similar
var serviceSpec = new ServiceCreateParameters
{
    Service = new ServiceSpec
    {
        Labels = new Dictionary<string, string>
        {
            [ServiceLabels.Managed] = ServiceLabels.ManagedValue, // "true"
            [ServiceLabels.ServerId] = serverId,
            [ServiceLabels.Name] = name,
            [ServiceLabels.GameType] = gameType
        },
        // ... rest of spec
    }
};
```

---

## 🧪 Test with a Simple Service

Create a test GameServer manually:

```bash
docker service create \
  --name test-minecraft \
  --label gameserver.docker.managed=true \
  --label gameserver.docker.Id=test-001 \
  --label gameserver.docker.name="Test Server" \
  --label gameserver.docker.gametype=minecraft \
  --replicas 1 \
  minecraft:latest
```

Then check the dashboard:
```bash
curl http://gameserver-docker:8080/api/dashboard/servers | jq
```

**Expected:** Should show 1 GameServer

---

## 📊 Verification Checklist

- [ ] Services exist in Docker Swarm (`docker service ls`)
- [ ] Services have **deploy labels** (not container labels)
- [ ] `gameserver.docker.managed` label exists
- [ ] `gameserver.docker.managed` value is `"true"` (string)
- [ ] Services were created/updated recently
- [ ] Dashboard API returns 200 (not 500)
- [ ] Enhanced logging shows label details

---

## 🔧 Quick Fix Commands

```bash
# 1. Find services WITHOUT the managed label
docker service ls --format "{{.Name}}" | while read svc; do
  labels=$(docker service inspect $svc --format '{{json .Spec.Labels}}')
  if ! echo "$labels" | grep -q "gameserver.docker.managed"; then
    echo "Missing label: $svc"
  fi
done

# 2. Add labels to all your GameServers
for svc in minecraft-server valheim-server terraria-server; do
  docker service update \
    --label-add gameserver.docker.managed=true \
    --label-add gameserver.docker.Id=$(uuidgen) \
    --label-add gameserver.docker.name="$svc" \
    --label-add gameserver.docker.gametype=$(echo $svc | cut -d'-' -f1) \
    $svc
done

# 3. Check dashboard
curl http://gameserver-docker:8080/api/dashboard/servers
```

---

## 💡 Next Steps

1. **Check your logs** with the enhanced diagnostics (commit c843368)
2. **Verify service labels** using commands above
3. **Add missing labels** to your GameServer services
4. **Test dashboard** - should now show servers

---

**The discovery logic is working!** The issue is likely that your services don't have the required labels yet. The enhanced logging will confirm this.

Let me know what you see in the logs after deploying this version! 🔍
