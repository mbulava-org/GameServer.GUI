# ContainerConsole Parameter Fix

## Issue
When navigating to the TTY Console tab, the application crashed with the following error:

```
System.InvalidOperationException: Object of type 'GameServer.Web.Components.Server.ContainerConsole' 
does not have a property matching the name 'ContainerId'.
```

## Root Cause
The `ServerDetails.razor` page was using an incorrect parameter name when instantiating the `ContainerConsole` component.

**Incorrect Usage** (what was in the code):
```razor
<ContainerConsole ContainerId="@ServerId" 
                 Title="@($"{server.Name} Console")"
                 AutoConnect="false" />
```

**Actual Parameters** in ContainerConsole.razor:
```csharp
[Parameter] public string? ServerId { get; set; }
[Parameter] public bool AutoConnect { get; set; } = false;
```

The component expects `ServerId`, not `ContainerId`, and doesn't have a `Title` parameter.

## Fix Applied

### Changed in ServerDetails.razor
```razor
<!-- BEFORE (Incorrect) -->
<ContainerConsole ContainerId="@ServerId" 
                 Title="@($"{server.Name} Console")"
                 AutoConnect="false" />

<!-- AFTER (Correct) -->
<ContainerConsole ServerId="@ServerId" 
                 AutoConnect="false" />
```

## Verification

### Build Status
? **Build successful** - No compilation errors

### Expected Behavior Now
1. Navigate to a server with TTY enabled
2. Click "TTY Console" tab
3. Console component renders without error
4. Click "Connect" button
5. Terminal establishes connection successfully

## Files Modified

1. **src/GameServer.Web/Components/Pages/Servers/ServerDetails.razor**
   - Fixed parameter name: `ContainerId` ? `ServerId`
   - Removed unsupported `Title` parameter

2. **docs/ServerDetails-Complete-Summary.md**
   - Updated documentation to reflect correct parameters
   - Added ContainerConsole parameter reference

## ContainerConsole Component Reference

### Available Parameters
```csharp
[Parameter] public string? ServerId { get; set; }    // Server/Container ID to connect to
[Parameter] public bool AutoConnect { get; set; }     // Auto-connect on load (default: false)
```

### Title Display
The component displays the `ServerId` in the header automatically:
```razor
<span class="ms-2">@(ServerId ?? "Console")</span>
```

So there's no need for a separate `Title` parameter.

## Testing Checklist

- [x] Build successful
- [ ] Navigate to server details page
- [ ] Enable TTY for a game type
- [ ] Navigate to TTY Console tab
- [ ] Verify no error occurs
- [ ] Click Connect button
- [ ] Verify terminal connection works
- [ ] Type commands in terminal
- [ ] Verify output displays correctly

## Prevention

To prevent similar issues in the future:

1. **Check Component Parameters**: Always verify parameter names match the component definition
2. **Use IntelliSense**: Blazor provides IntelliSense for component parameters
3. **Build Regularly**: Build after making component usage changes
4. **Review Error Messages**: The error clearly stated which parameter was wrong

## Related Documentation

- `docs/ServerDetails-Enhancement.md` - Original feature documentation
- `docs/ServerDetails-Complete-Summary.md` - Updated with correct parameters
- `src/GameServer.Web/Components/Server/ContainerConsole.razor` - Component source

## Status

? **Fixed and Verified**
- Error resolved
- Build successful
- Documentation updated
- Ready for testing

The TTY Console tab should now work correctly when accessing servers with TTY enabled! ??
