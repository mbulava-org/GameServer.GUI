using Docker.DotNet;
using GameServer.Docker.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using Docker.DotNet.Models;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Container operations controller
    /// </summary>
    [ApiController]
    [Route("containers")]
    [Route("api/containers")]
    public class ContainersController : ControllerBase
    {
        private readonly IContainerService _containerService;
        private readonly ILogger<ContainersController> _logger;

        public ContainersController(IContainerService containerService, ILogger<ContainersController> logger)
        {
            _containerService = containerService;
            _logger = logger;
        }

        /// <summary>
        /// Get real-time statistics for a specific container
        /// </summary>
        [HttpGet("{id}/stats")]
        [ProducesResponseType(200, Type = typeof(Models.ContainerStatsResponse))]
        [ProducesResponseType(404, Type = typeof(Models.ErrorResponse))]
        [ProducesResponseType(408, Type = typeof(Models.ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetContainerStats(string id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Getting stats for container {ContainerId}", id);
                var stats = await _containerService.GetContainerStatsAsync(id, cancellationToken);
                return Ok(stats);
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found on this node", id);
                return NotFound(new Models.ErrorResponse { Error = $"Container {id} not found on this node" });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Stats collection timed out for container {ContainerId}", id);
                return StatusCode(408, new Models.ErrorResponse { Error = $"Stats collection timed out for container {id}" });
            }
            catch (Exception ex)
            {
                // Log as warning to avoid noisy error logs when stats collection fails
                _logger.LogWarning(ex, "Error getting stats for container {ContainerId}", id);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Get logs from a specific container
        /// </summary>
        [HttpGet("{id}/logs")]
        [ProducesResponseType(200, Type = typeof(Models.ContainerLogsResponse))]
        [ProducesResponseType(404, Type = typeof(Models.ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetContainerLogs(
            string id,
            [FromQuery] int tail = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting logs for container {ContainerId}, tail={Tail}", id, tail);
                var logs = await _containerService.GetContainerLogsAsync(id, tail, cancellationToken);
                return Ok(logs);
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found on this node", id);
                return NotFound(new Models.ErrorResponse { Error = $"Container {id} not found on this node" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs for container {ContainerId}", id);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Inspect a specific container to get detailed information
        /// </summary>
        [HttpGet("{id}/inspect")]
        [ProducesResponseType(200, Type = typeof(Models.ContainerInspectResponse))]
        [ProducesResponseType(404, Type = typeof(Models.ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> InspectContainer(string id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Inspecting container {ContainerId}", id);
                var info = await _containerService.InspectContainerAsync(id, cancellationToken);
                return Ok(info);
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found on this node", id);
                return NotFound(new Models.ErrorResponse { Error = $"Container {id} not found on this node" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inspecting container {ContainerId}", id);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// List all running containers on this node
        /// </summary>
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(Models.ContainerListResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ListContainers(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Listing containers on this node");
                var containers = await _containerService.ListContainersAsync(cancellationToken);
                return Ok(containers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing containers");
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Attach to container console via WebSocket (bidirectional TTY access)
        /// </summary>
        [HttpGet("{id}/attach/ws")]
        public async Task AttachToContainerWebSocket(string id)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            _logger.LogInformation("WebSocket attach request for container {ContainerId}", id);

            try
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established for container {ContainerId}", id);

                // Create a multiplexed stream for Docker attach including previous logs
                var parameters = new ContainerAttachParameters
                {
                    Stream = true,
                    Stdin = true,
                    Stdout = true,
                    Stderr = true,
                    Logs = true
                };

                var dockerClient = HttpContext.RequestServices.GetRequiredService<IDockerClient>();
                using var stream = await dockerClient.Containers.AttachContainerAsync(id, parameters, CancellationToken.None);

                // Start bidirectional forwarding
                var receiveTask = ReceiveFromWebSocketAsync(webSocket, stream, id);
                var sendTask = SendToWebSocketAsync(webSocket, stream, id);

                await Task.WhenAny(receiveTask, sendTask);

                _logger.LogInformation("WebSocket session ended for container {ContainerId}", id);
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found during attach", id);
                HttpContext.Response.StatusCode = 404;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WebSocket attach to container {ContainerId}", id);
                HttpContext.Response.StatusCode = 500;
            }
        }

        /// <summary>
        /// Execute a command in the container and return output (non-interactive)
        /// </summary>
        [HttpPost("{id}/exec")]
        [ProducesResponseType(200, Type = typeof(Models.ExecResponse))]
        [ProducesResponseType(404, Type = typeof(Models.ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ExecCommand(
            string id,
            [FromBody] Models.ExecRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing non-interactive command in container {ContainerId}: {Command}",
                    id, string.Join(" ", request.Cmd ?? Array.Empty<string>()));

                var dockerClient = HttpContext.RequestServices.GetRequiredService<IDockerClient>();

                // Create exec instance (non-interactive)
                var execCreateParams = new ContainerExecCreateParameters
                {
                    Cmd = request.Cmd,
                    AttachStdout = request.AttachStdout,
                    AttachStderr = request.AttachStderr,
                    AttachStdin = false,  // No stdin for non-interactive
                    TTY = false
                };

                var execCreateResponse = await dockerClient.Exec.CreateContainerExecAsync(id, execCreateParams, cancellationToken);

                // Start exec and get output
                using var stream = await dockerClient.Exec.StartContainerExecAsync(
                    execCreateResponse.ID,
                    new ContainerExecStartParameters { Detach = false, TTY = false },
                    cancellationToken);

                // Read output
                var output = new StringBuilder();
                var buffer = new byte[4096];

                while (true)
                {
                    var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (result.Count == 0) break;

                    output.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                // Inspect to get exit code
                var inspect = await dockerClient.Exec.InspectContainerExecAsync(execCreateResponse.ID, cancellationToken);

                return Ok(new Models.ExecResponse
                {
                    ExitCode = (int)inspect.ExitCode,
                    Output = output.ToString()
                });
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found for exec", id);
                return NotFound(new Models.ErrorResponse { Error = $"Container {id} not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command in container {ContainerId}", id);
                return StatusCode(500, new Models.ErrorResponse { Error = ex.Message });
            }
        }

        /// <summary>
        /// Execute an interactive command in the container via WebSocket (with stdin support)
        /// </summary>
        [HttpGet("{id}/exec/ws")]
        public async Task ExecInteractiveWebSocket(
            string id,
            [FromQuery] string[] cmd,
            [FromQuery] bool tty = false)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = 400;
                return;
            }

            var command = (cmd != null && cmd.Length > 0) ? cmd : new[] { "/bin/sh" };

            _logger.LogInformation("WebSocket exec request for container {ContainerId}: {Command}",
                id, string.Join(" ", command));

            try
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established for exec in container {ContainerId}", id);

                var dockerClient = HttpContext.RequestServices.GetRequiredService<IDockerClient>();

                // Create exec instance with stdin/stdout/stderr
                var execCreateParams = new ContainerExecCreateParameters
                {
                    Cmd = command,
                    AttachStdout = true,
                    AttachStderr = true,
                    AttachStdin = true,   // Enable stdin for interactive session
                    TTY = tty
                };

                var execCreateResponse = await dockerClient.Exec.CreateContainerExecAsync(id, execCreateParams, CancellationToken.None);

                // Start exec with multiplexed stream
                using var stream = await dockerClient.Exec.StartContainerExecAsync(
                    execCreateResponse.ID,
                    new ContainerExecStartParameters { Detach = false, TTY = tty },
                    CancellationToken.None);

                // Start bidirectional forwarding (same as attach)
                var receiveTask = ReceiveFromWebSocketAsync(webSocket, stream, id);
                var sendTask = SendToWebSocketAsync(webSocket, stream, id);

                await Task.WhenAny(receiveTask, sendTask);

                // Get exit code after exec completes
                try
                {
                    var execInspect = await dockerClient.Exec.InspectContainerExecAsync(execCreateResponse.ID, CancellationToken.None);
                    _logger.LogInformation("Exec completed in container {ContainerId} with exit code {ExitCode}",
                        id, execInspect.ExitCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not inspect exec {ExecId} for exit code", execCreateResponse.ID);
                }

                _logger.LogInformation("WebSocket exec session ended for container {ContainerId}", id);
            }
            catch (DockerContainerNotFoundException)
            {
                _logger.LogWarning("Container {ContainerId} not found during exec", id);
                // Cannot set StatusCode - WebSocket already upgraded
                // Just log the error - client will see WebSocket close
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WebSocket exec in container {ContainerId}", id);
                // Cannot set StatusCode - WebSocket already upgraded
                // Just log the error - client will see WebSocket close
            }
        }

        /// <summary>
        /// Receive data from WebSocket and send to container stdin
        /// </summary>
        private async Task ReceiveFromWebSocketAsync(WebSocket webSocket, MultiplexedStream dockerStream, string containerId)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Write to container stdin
                        await dockerStream.WriteAsync(buffer, 0, result.Count, CancellationToken.None);
                        _logger.LogTrace("Sent {ByteCount} bytes to container {ContainerId} stdin", result.Count, containerId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket client closed connection for container {ContainerId}", containerId);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error receiving from WebSocket for container {ContainerId}", containerId);
            }
        }

        /// <summary>
        /// Read from container stdout/stderr and send to WebSocket
        /// </summary>
        private async Task SendToWebSocketAsync(WebSocket webSocket, MultiplexedStream dockerStream, string containerId)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await dockerStream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);

                    if (result.Count > 0)
                    {
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(buffer, 0, result.Count),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);

                        _logger.LogTrace("Sent {ByteCount} bytes from container {ContainerId} to WebSocket", result.Count, containerId);
                    }
                    else
                    {
                        // No more data
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending to WebSocket for container {ContainerId}", containerId);
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stream ended", CancellationToken.None);
                }
            }
        }
    }
}
