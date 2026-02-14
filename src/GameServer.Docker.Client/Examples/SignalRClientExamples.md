# GameServer.Docker SignalR Client Examples

## Console Client Usage

### Basic Console Example
using GameServer.Docker.Client.Services; 
using Microsoft.Extensions.Logging;
// Create logger factory (optional) using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole()); var logger = loggerFactory.CreateLogger<ServerConsoleClient>();
// Create console client var consoleClient = new ServerConsoleClient( "https://localhost:5001/hubs/console", logger);
// Subscribe to events consoleClient.ConsoleOutput += (sender, output) => { Console.Write(output); };
consoleClient.ConsoleError += (sender, error) => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"ERROR: {error}"); Console.ResetColor(); };
consoleClient.ConsoleStarted += (sender, serverId) => { Console.WriteLine($"Console session started for server: {serverId}"); };
consoleClient.StateChanged += (sender, state) => { Console.WriteLine($"Connection state: {state}"); };
// Connect and start console await consoleClient.ConnectAsync(); await consoleClient.StartConsoleAsync("my-server-id");
// Send commands await consoleClient.SendInputAsync("ls -la\n"); await consoleClient.SendInputAsync("pwd\n"); await consoleClient.SendInputAsync("help\n");
// Keep alive and accept user input while (true) { var input = Console.ReadLine(); if (input == "exit") break;
await consoleClient.SendInputAsync(input + "\n");
}
// Cleanup await consoleClient.StopConsoleAsync(); await consoleClient.DisconnectAsync(); await consoleClient.DisposeAsync();
