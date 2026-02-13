# Quick Start - Testing the New Features

## ?? Quick Test (5 minutes)

### 1. View Both Monitors (1 min)
```bash
# Start your application
cd GameServer.Web
dotnet run
```

1. Navigate to: `https://localhost:5001/servers`
2. Click any server
3. ? **Verify**: Both "Real-Time Monitor (SignalR)" and "REST API Monitor" appear
4. ? **Verify**: Both monitors show data and update

### 2. Test REST Monitor Features (2 min)
1. Click the **Refresh** button on REST API Monitor
2. ? **Verify**: "Refreshing..." badge appears
3. ? **Verify**: Data updates and timestamp changes
4. Wait 5 seconds
5. ? **Verify**: Monitor auto-refreshes

### 3. Test TTY Console Tab (2 min)

#### Enable TTY for a Game Type
1. Navigate to: `https://localhost:5001/gametypes`
2. Click any game type (e.g., "Minecraft")
3. Scroll to "Advanced Settings"
4. ? **Check** the "Enable TTY" checkbox
5. Click **Save**

#### Access TTY Console
1. Navigate back to a server of that game type
2. ? **Verify**: New "TTY Console" tab appears
3. Click the **TTY Console** tab
4. If server is running:
   - ? **Verify**: ContainerConsole component appears
   - Click **Connect**
   - ? **Verify**: Terminal connects
5. If server is stopped:
   - ? **Verify**: Message shows "Server must be running..."

## ?? What You Should See

### Both Monitors Running
```
??????????????????????????????????????
? Real-Time Monitor (SignalR)        ?
? [Live] [Stop]                      ?
?                                    ?
? CPU: 45.2%    Memory: 62.1%        ?
? Network RX/TX    Disk Read/Write   ?
? [Historical Chart]                 ?
??????????????????????????????????????

??????????????????????????????????????
? REST API Monitor                   ?
? [Updated 14:32:45] [Refresh]       ?
?                                    ?
? Status: Running    Replicas: 1/1   ?
? Health: 100%       CPU: 2.0 CPUs   ?
? Memory: 4 GB       Container: abc  ?
??????????????????????????????????????
```

### TTY Console Tab (When Enabled)
```
[Overview] [Logs] [Files] [TTY Console]  ? New tab!
                             ?
                        Only appears if EnableTTY = true
```

## ? Quick Troubleshooting

### "REST Monitor shows error"
- **Check**: Is GameServer.Docker API running?
- **Fix**: Start the API service

### "TTY Console tab not showing"
- **Check**: Did you enable TTY in game type metadata?
- **Check**: Did you refresh the page after enabling?
- **Fix**: Edit game type ? Enable TTY ? Save ? Refresh page

### "Can't connect to TTY console"
- **Check**: Is the server running?
- **Fix**: Start the server first, then try connecting

### "Monitors not updating"
- **Check**: Browser console for errors (F12)
- **Fix**: Refresh the page

## ?? Compare the Monitors

Watch both monitors side-by-side and notice:

| Feature | Real-Time (SignalR) | REST API |
|---------|-------------------|----------|
| Updates | Every 2 seconds | Every 5 seconds |
| Shows | Live CPU/Memory % | Service Status |
| Shows | Network/Disk I/O | Resource Limits |
| Shows | Historical Chart | Replica Health |

They show **different but complementary** data! ??

## ? Success Criteria

You've successfully tested the features when:
- [x] Both monitors appear on server details page
- [x] Both monitors update independently
- [x] REST monitor refreshes manually and automatically
- [x] TTY Console tab appears for TTY-enabled game types
- [x] TTY Console tab hidden for non-TTY game types
- [x] Console connects when server is running
- [x] Console shows message when server is stopped

## ?? Key Takeaways

1. **Two Monitors**: Real-time metrics + service configuration
2. **Side-by-Side**: Compare live usage vs limits
3. **TTY Console**: Interactive terminal for enabled game types
4. **Conditional**: TTY tab only when EnableTTY = true
5. **Auto-Updates**: SignalR (2s) + REST (5s) keep data fresh

## ?? Full Documentation

For detailed information, see:
- `docs/ServerDetails-Complete-Summary.md` - Full feature overview
- `docs/ServerDetails-Enhancement.md` - Technical details
- `docs/ServerDetails-Testing-Guide.md` - Comprehensive tests
- `docs/ServerDetails-Visual-Guide.md` - Visual layouts

## ?? Ready for Production?

Before deploying:
- [ ] Run full test suite from Testing Guide
- [ ] Test on different screen sizes
- [ ] Verify performance is acceptable
- [ ] Test with multiple concurrent users
- [ ] Verify no console errors

---

**Need help?** Check the documentation or run the comprehensive test suite! ??
