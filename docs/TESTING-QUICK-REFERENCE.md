# ?? Quick Reference - What's New & How to Test

**Branch:** port-mapping  
**Status:** ? Ready for Testing  
**Restart Required:** Yes (SignalR hub changes)

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

### 3. ?? Real Log Streaming  
**What:** Live container logs via SignalR  
**Where:** ServerDetails ? Logs tab  
**Test:** Open logs tab, click "Stream Logs", verify real output

### 4. ?? Interactive Terminal (NEW!)  
**What:** Web-based shell with /bin/sh exec  
**Where:** ServerDetails ? Terminal tab  
**Test:** Open terminal tab (will show connection error until hub implemented)

### 5. ?? Settings Auto-Display  
**What:** All DefaultSettings shown even without metadata  
**Where:** CreateServerWizard ? Game Settings step  
**Test:** Create any server, verify all settings visible in tabs

---

## ?? Testing Workflow

### Quick Test (5 minutes)

1. **Restart Application** (code changes applied)
2. **Navigate to:** `/gametypes/valheim`
3. **Expand:** SERVER_PORT setting
4. **Click:** "Auto-Detect" in Port Relationships
5. **Verify:** 2 relationships created (Offset +1 and +2)
6. **Navigate to:** `/servers/new`
7. **Create:** Valheim server
8. **Change:** SERVER_PORT from 2456 to 30000 in Step 3
9. **Go to:** Step 4 (Technical Details)
10. **Verify:** Ports are 30000, 30001 ?, 30002
11. **Go to:** Step 5 (Review)
12. **Verify:** Connection string shows `IP:30001` (default port)
13. **Create** server
14. **Go to:** ServerDetails ? Logs
15. **Click:** "Stream Logs"
16. **Verify:** Real logs appear

### Full Test (15 minutes)

Include Quick Test above, plus:

1. **Edit** existing server
2. **Verify:** Port relationships preserved
3. **Test:** Terminal tab (expect connection error - hub not implemented yet)
4. **Test:** Network section shows default port correctly
5. **Test:** Home page - all links work
6. **Browse:** Documentation at `docs/CURRENT-FEATURES.md`

---

## ?? Key Locations

### User-Facing
- **Home:** `/` - Updated with new features
- **Servers:** `/servers` - Server list
- **Create:** `/servers/new` - 5-step wizard
- **Details:** `/servers/{id}` - Tabs: Overview, Logs, Terminal, Files, TTY Console
- **GameTypes:** `/gametypes` - Game type management
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

### ?? Not Yet Implemented
- Terminal hub backend (`/hubs/terminal`)
  - Component is ready
  - Hub needs to be created
  - Will show connection error until implemented

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

### Default Port Wrong?
1. Check GameType has `IsDefaultPort = true` on correct port
2. Verify port order matches GameType definition
3. Check GameTypeDefinition passed to PortMappingEditor

### Port Relationships Not Working?
1. Check metadata has `MapsToContainerPort = true`
2. Verify `LinkedContainerPort` matches port in Ports list
3. Check `PortRelationships` array populated
4. Run "Auto-Detect" to regenerate

---

## ?? Before Committing

- [x] Build successful
- [x] Documentation updated
- [x] Home page updated
- [ ] Manual testing completed
- [ ] Logs streaming works
- [ ] Port relationships work correctly
- [ ] Default port displayed everywhere

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

? Valheim server creates with 3 ports  
? Port 2457 marked as default  
? Changing SERVER_PORT updates all 3 ports  
? Connection string uses default port (2457/30001)  
? Logs stream in real-time  
? Default port indicator shows everywhere  
? All links on Home page work  

---

**Ready to Test! ??**

Start with Quick Test, then Full Test. Report any issues found.
