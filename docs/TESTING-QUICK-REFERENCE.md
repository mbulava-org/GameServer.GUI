# Quick Reference - How to Test

**Branch:** main  
**Status:** Release Readiness — V1 Decommission & Shared Streaming  
**Restart Required:** Yes (SignalR hubs, V2 UI, and Docker client package changes)

---

## ? New Features This Session

### 1. ?? Smart Port Management
**What:** Automatic port relationships (Offset, Fixed, Multiplier)  
**Where:** GameType Editor ? Settings Metadata ? Port Relationships  
**Test:** Edit Valheim game type, click "Auto-Detect", create server, change SERVER_PORT

### 2. ?? Default Port System  
**What:** Primary connection port highlighted with star ?  
**Where:** Everywhere ports are shown  
**Test:** Create Valheim server, verify port 2457 (not 2456) is marked as default

### 3. ?? Shared Log Streaming  
**What:** Live container logs via `IServerLogAggregator` (`/hubs/serverlogs`)  
**Where:** Server Details → Logs tab  
**Test:** Open logs tab, click "Stream Logs", verify real output. Open a second browser and confirm both see the same stream.

### 4. Shared Container Attach  
**What:** TTY attach output shared across viewers; first typist wins input control (`/hubs/attach`)  
**Where:** Server Details → Console tab  
**Test:** Open console tab in two browsers; both should see identical output. The first user to type should get the "Input Control" badge; the second should see "View-only".

### 5. Interactive Terminal (Per-User Exec)
**What:** Web-based shell with `/bin/sh` exec via `ContainerConsoleHub` (`/hubs/terminal`)  
**Where:** Server Details → Terminal tab  
**Test:** Open terminal tab, type `ls`, and verify command output. A second user opening Terminal should get an independent shell.

### 6. V2 GameServer Pages
**What:** Active V2 server list, detail, and create/edit pages  
**Where:** `/gameservers-v2`, `/gameservers-v2/new`, `/gameservers-v2/{id}`, `/gameservers-v2/{id}/edit`  
**Test:** Create a V2 server from the list page, view details, then edit it

### 7. Settings Auto-Display  
**What:** All DefaultSettings shown even without metadata  
**Where:** CreateServerWizard ? Game Settings step  
**Test:** Create any server, verify all settings visible in tabs

### 8. Docker.DotNet.Enhanced 4.3.3
**What:** Agent and primary service use `Docker.DotNet.Enhanced` matching Testcontainers 4.x  
**Where:** `src/GameServer.Docker.Agent`, `src/GameServer.Docker`  
**Test:** Build solution in Release; run `GameServer.Docker.Agent.Tests`

---

## ?? Testing Workflow

### Quick Test (5 minutes)

1. **Restart Application** (code changes applied)
2. **Navigate to:** `/gametypes-v2`
3. **Select/Create:** Valheim game type with a published revision
4. **Edit revision:** Expand SERVER_PORT setting and click **Auto-Detect** in Port Relationships
5. **Verify:** 2 relationships created (Offset +1 and +2)
6. **Navigate to:** `/gameservers-v2/new`
7. **Create:** Valheim V2 server
8. **Change:** SERVER_PORT from 2456 to 30000
9. **Verify:** Port previews update to 30000, 30001, 30002
10. **Create** server
11. **Go to:** Server Details → Logs
12. **Click:** "Stream Logs"
13. **Verify:** Real logs appear
14. **Open a second browser/session to the same Logs tab**
15. **Verify:** Both sessions see the same output
16. **Go to:** Server Details → Terminal
17. **Type:** `ls`
18. **Verify:** Command output appears

### Full Test (15 minutes)

Include Quick Test above, plus:

1. **Edit** existing V2 server and change its revision
2. **Verify:** Port relationships preserved
3. **Test:** Terminal tab and run a command
4. **Test:** Console tab (if TTY enabled on the game type)
5. **Open Console tab in two browsers**
6. **Verify:** Both see the same output; first typist gets "Input Control", second gets "View-only" badge
7. **Test:** Network section shows default port correctly
8. **Test:** Resource monitoring shows live stats
9. **Test:** Home page - all links work (now point to V2 paths)
10. **Browse:** Documentation at `docs/CURRENT-FEATURES.md`

---

## ?? Key Locations

### User-Facing
- **Home:** `/` - Updated with V2 navigation
- **Servers (V2, active):** `/gameservers-v2` - V2 server list
- **Create Server (V2):** `/gameservers-v2/new` - V2 create/edit editor
- **Server Details (V2):** `/gameservers-v2/{id}` - V2 details
- **GameTypes (V2):** `/gametypes-v2` - Game type management
- **GameType Editor:** `/gametypes/{key}` - Port relationships here

### Configuration
- `appsettings.Development.json` ? `GameServerDockerApi:BaseUri`

### Documentation
- `docs/CURRENT-FEATURES.md` - Complete feature list
- `docs/SESSION-SUMMARY.md` - This session's changes

---

## ?? Configuration Check

```json
{
  "GameServerDockerApi": {
    "BaseUri": "http://192.168.10.50:5163/"  // ? Check this!
  }
}
```

---

## ?? Expected Issues

### ? Working
- Port relationships
- Default port display
- Log streaming
- Settings display
- All existing features

### Not Yet Implemented / Known Limitations
- V2 server start/stop/delete actions are delegated to service operations, not explicit V2 API endpoints
- PostgreSQL V2 support exists in code but is not fully implemented / production-ready

---

## ?? What to Look For

### Port Display Should Show:
```
? 30001 ? 30001 (udp) ?  [Green badge - Default]
  30000 ? 30000 (udp)     [Blue badge]
  30002 ? 30002 (udp)     [Blue badge]
```

### Connection String Should Show:
```
192.168.10.50:30001  (not 30000!)
```

### Logs Tab Should Show:
```
Real container output with timestamps
Clean text (no binary characters)
Auto-scrolling if enabled
```

---

## ?? Troubleshooting

### Logs Not Working?
1. Check API base URI in `appsettings.Development.json`
2. Verify server is running
3. Check browser console for errors
4. Verify SignalR hub at `{API}/hubs/serverlogs`
5. If multiple viewers see different content, ensure they requested the same `tailLines`

### Shared Console (Attach) Not Working?
1. Confirm the client connects to `{API}/hubs/attach`, not the old `/hubs/console`
2. Verify the container is running and the agent exposes `/containers/{id}/attach/ws`
3. Check that `SendInput` only succeeds after a controlling user is established
4. Verify the UI toggles the "View-only" / "Input Control" badges on `InputControlChanged`

### Default/Advertised Port Wrong?
1. Check the active `GameTypeRevision` has `IsAdvertised = true` on the correct port
2. Verify port order matches the revision definition
3. Check the revision is passed to the port mapping editor

### Port Mappings Not Working?
1. Check the setting metadata `DataType` is `"port"`
2. Verify the primary direct mapping targets a port in the revision's `Ports` list
3. Check related offset/multiplier mappings derive from the primary mapping
4. Run "Auto-Detect" to regenerate

---

## ?? Before Committing

- [x] Build successful
- [x] Documentation updated
- [x] Home page updated
- [ ] Manual testing completed
- [ ] Logs streaming works (shared across multiple clients)
- [ ] Shared container attach works (input control badge, view-only for second user)
- [ ] Per-user terminal exec works
- [ ] Port mappings work correctly
- [ ] Advertised port displayed everywhere

---

## ?? Quick Commands

```bash
# Build
dotnet build

# Run Web App
cd src\GameServer.Web
dotnet run

# Run API
cd src\GameServer.Docker
dotnet run

# View logs
docker service logs {service-name}

# Check container labels
docker inspect {container-id} --format '{{json .Config.Labels}}'
```

---

## ?? Success Criteria

- Valheim server creates with 3 ports
- Port 2457 marked as default
- Changing SERVER_PORT updates all 3 ports
- Connection string uses default port (2457/30001)
- Logs stream in real-time and are shared across viewers
- Shared container attach streams identical output; input control badge updates correctly
- Terminal accepts commands and returns output (per-user exec)
- Default port indicator shows everywhere
- All links on Home page work and point to V2 paths

---

**Ready to Test! ??**

Start with Quick Test, then Full Test. Report any issues found.
