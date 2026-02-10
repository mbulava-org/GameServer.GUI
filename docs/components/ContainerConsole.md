# ContainerConsole Component

A professional SSH terminal-like Blazor component for interacting with Docker container consoles in real-time.

## Features

? **Terminal Appearance**
- Dark theme with monospace font
- Green/colored text output based on message type
- Blinking cursor animation
- Smooth scrolling with custom scrollbar

? **Real-Time Console**
- WebSocket/SignalR connection to container console
- Live output streaming
- Command execution
- Connection state management

? **User Experience**
- Command history (up/down arrows)
- Keyboard shortcuts (Ctrl+L, Ctrl+C)
- Auto-scroll to latest output
- Input focus management
- Connection status indicators

? **Message Types**
- Normal output (white)
- Errors (red)
- Warnings (yellow)
- Success (green)
- Info (blue)
- System messages (gray, italic)

## Usage

### Basic Usage

```razor
<ContainerConsole ServerId="my-server-123" AutoConnect="true" />
```

### In a Page

```razor
@page "/servers/{ServerId}/console"
@using GameServer.Web.Components.Server

<PageTitle>Server Console</PageTitle>

<div class="container mt-4">
    <ContainerConsole ServerId="@ServerId" AutoConnect="true" />
</div>

@code {
    [Parameter] public string ServerId { get; set; } = "";
}
```

### Programmatic Control

```razor
<ContainerConsole ServerId="@serverId" 
                 AutoConnect="false" 
                 @ref="console" />

<RadzenButton Text="Connect" Click="@(() => console?.ConnectAsync())" />

@code {
    private ContainerConsole? console;
    private string serverId = "my-server";
}
```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ServerId` | `string?` | `null` | The ID of the game server to connect to |
| `AutoConnect` | `bool` | `false` | Automatically connect when component loads |

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Enter` | Send command |
| `?` | Previous command in history |
| `?` | Next command in history |
| `Ctrl+L` | Clear console output |
| `Ctrl+C` | Disconnect from console |

## API Integration

### Required Interface: `IContainerConsoleClient`

The component uses the `IContainerConsoleClient` interface from `GameServer.Docker.Client`:

```csharp
public interface IContainerConsoleClient
{
    event Action<string>? OnMessageReceived;
    event Action<bool>? OnConnectionStateChanged;
    event Action<string>? OnError;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendCommandAsync(string command);
    bool IsConnected { get; }
}
```

### Expected API Method

The component expects the following method on `IGameServerApi`:

```csharp
Task<ConsoleConnectionInfo> GetConsoleInfoAsync(string serverId);

public class ConsoleConnectionInfo
{
    public string Url { get; set; }  // WebSocket/SignalR URL
}
```

## Customization

### Styling

The component uses CSS custom properties for theming:

```css
.terminal-container {
    --terminal-bg: #1e1e1e;
    --terminal-fg: #d4d4d4;
    --terminal-header-bg: #2d2d2d;
    --terminal-prompt-color: #4ec9b0;
}
```

### Message Type Colors

```css
.terminal-line.error { color: #f48771; }    /* Red */
.terminal-line.warning { color: #dcdcaa; }  /* Yellow */
.terminal-line.success { color: #4ec9b0; }  /* Green */
.terminal-line.info { color: #9cdcfe; }     /* Blue */
.terminal-line.system { color: #6a9955; }   /* Gray */
```

## State Management

### Connection States

- **Disconnected** (red badge) - Not connected, shows Connect button
- **Connecting** (yellow badge) - Connection in progress
- **Connected** (green badge) - Active connection, shows Disconnect button

### Output Management

- Stores last 1000 lines of output
- Older lines automatically removed
- Each line includes timestamp
- Auto-scrolls to show latest output

### Command History

- Stores last 50 commands
- Navigate with Up/Down arrow keys
- Persists during session
- Cleared on disconnect

## Example: Full Integration

```razor
@page "/servers/{ServerId}/manage"
@using GameServer.Web.Components.Server

<RadzenTabs>
    <Tabs>
        <RadzenTabsItem Text="Overview">
            <!-- Server overview -->
        </RadzenTabsItem>
        
        <RadzenTabsItem Text="Console">
            <div class="p-4">
                <ContainerConsole ServerId="@ServerId" AutoConnect="true" />
            </div>
        </RadzenTabsItem>
        
        <RadzenTabsItem Text="Settings">
            <!-- Server settings -->
        </RadzenTabsItem>
    </Tabs>
</RadzenTabs>

@code {
    [Parameter] public string ServerId { get; set; } = "";
}
```

## Backend Implementation Example

### SignalR Hub

```csharp
public class ContainerConsoleHub : Hub
{
    private readonly IContainerService _containerService;

    public async Task ConnectToContainer(string serverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serverId);
        
        // Attach to container output
        _containerService.AttachToContainer(serverId, (output) =>
        {
            Clients.Group(serverId).SendAsync("ReceiveOutput", output);
        });
    }

    public async Task SendCommand(string serverId, string command)
    {
        await _containerService.ExecuteCommand(serverId, command);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Cleanup
        await base.OnDisconnectedAsync(exception);
    }
}
```

### API Endpoint

```csharp
[HttpGet("{serverId}/console")]
public async Task<ActionResult<ConsoleConnectionInfo>> GetConsoleInfo(string serverId)
{
    var server = await _serverRepository.GetAsync(serverId);
    if (server == null)
        return NotFound();

    return Ok(new ConsoleConnectionInfo
    {
        Url = $"ws://{Request.Host}/console-hub?serverId={serverId}"
    });
}
```

## Troubleshooting

### Console Not Connecting

1. Verify `ServerId` is correct
2. Check API endpoint returns valid console URL
3. Ensure WebSocket/SignalR is configured in backend
4. Check browser console for connection errors

### No Output Shown

1. Verify container is running
2. Check backend is streaming output
3. Ensure `OnMessageReceived` event is firing
4. Check browser console for JavaScript errors

### Commands Not Working

1. Verify `SendCommandAsync` is implemented
2. Check container accepts stdin
3. Ensure proper permissions
4. Verify command syntax for specific game server

## Performance Considerations

- **Output Buffer**: Limited to 1000 lines
- **Command History**: Limited to 50 commands
- **Auto-scroll**: Uses smooth scrolling behavior
- **Connection**: Automatic reconnection not implemented (add if needed)

## Security Considerations

- **Authentication**: Ensure users are authorized to access console
- **Command Validation**: Consider server-side command filtering
- **Rate Limiting**: Implement to prevent command spam
- **Audit Logging**: Log all commands executed

## Future Enhancements

- [ ] Auto-reconnection on connection loss
- [ ] File upload/download via console
- [ ] Tab completion for commands
- [ ] Console log export/download
- [ ] Multiple console windows (tabs)
- [ ] Search/filter in output
- [ ] Color scheme customization
- [ ] Font size adjustment

## License

Part of GameServer.Web project.
