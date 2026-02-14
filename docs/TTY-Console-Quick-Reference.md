# TTY Console - Quick Reference Card

## ?? What Was Fixed

### Problem 1: Wrong Parameter
```diff
- <ContainerConsole ContainerId="@ServerId" Title="..." />
+ <ContainerConsole ServerId="@ServerId" AutoConnect="false" />
```

### Problem 2: Missing JavaScript
```diff
<!-- App.razor -->
+ <link href="_content/XtermBlazor/XtermBlazor.min.css" rel="stylesheet" />
+ <script src="_content/XtermBlazor/XtermBlazor.min.js"></script>
```
**Note**: Must use `.min.css` and `.min.js` (minified versions only)

## ? Quick Test (2 min)

1. **Enable TTY for a Game Type**
   ```
   Game Types ? Select Game ? Extended Metadata ? ? Enable TTY ? Save
   ```

2. **Test Console**
   ```
   Servers ? Select Server ? TTY Console Tab ? Connect
   ```

3. **Expected Result**
   ```
   ? Terminal renders
   ? No JavaScript errors
   ? Can type commands
   ```

## ?? Component Parameters

```csharp
// ContainerConsole.razor accepts:
[Parameter] public string? ServerId { get; set; }    // Required
[Parameter] public bool AutoConnect { get; set; }     // Default: false
```

## ?? Files Changed

1. ? `src/GameServer.Web/Components/Pages/Servers/ServerDetails.razor`
   - Fixed parameter names

2. ? `src/GameServer.Web/Components/App.razor`
   - Added XtermBlazor CSS
   - Added XtermBlazor JS

## ? Verification

```bash
# Build Check
dotnet build
# Expected: Build succeeded

# Browser Check (F12 Console)
# Expected: No "XtermBlazor" errors
```

## ?? Troubleshooting

| Problem | Fix |
|---------|-----|
| Tab doesn't appear | Check EnableTTY = true |
| JavaScript error | Clear cache, hard refresh |
| Can't connect | Ensure server is running |
| Terminal blank | Check console for errors |

## ?? Full Docs

- `docs/TTY-Console-All-Fixes.md` - Complete guide
- `docs/XtermBlazor-JavaScript-Fix.md` - JS setup details
- `docs/ContainerConsole-Parameter-Fix.md` - Parameter fix

## ?? Status

? All issues fixed  
? Build successful  
? Ready for testing

---

**Quick Help**: All files in `docs/` folder have detailed troubleshooting guides!
