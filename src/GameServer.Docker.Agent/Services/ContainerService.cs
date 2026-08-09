using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Interfaces;
using System.Runtime.CompilerServices;
using DockerStatsResponse = Docker.DotNet.Models.ContainerStatsResponse;

namespace GameServer.Docker.Agent.Services
{
    /// <summary>
    /// Service for interacting with Docker containers on the local node
    /// </summary>
    public class ContainerService : IContainerService
    {
        private const int DefaultStatsTimeoutSeconds = 10;

        private readonly IDockerClient _dockerClient;
        private readonly ILogger<ContainerService> _logger;

        public ContainerService(IDockerClient dockerClient, ILogger<ContainerService> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        public async Task<Models.ContainerStatsResponse> GetContainerStatsAsync(string containerId, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting stats for container {ContainerId}", containerId);

            // Use Progress to capture stats response
            //var statsResponse = new TaskCompletionSource<ContainerStatsResponse>();
            var response = new ContainerStatsResponse();
            var progress = new Progress<ContainerStatsResponse>(stats =>
            {
                _logger.LogTrace("Received stats from Docker for container {ContainerId}", containerId);
                response = stats;
            });
            progress.ProgressChanged += (s, e) => {
                _logger.LogTrace("Received progresschanged from Docker for container {ContainerId}", containerId);
                response = e;
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Set a timeout to prevent hanging forever
            cts.CancelAfter(TimeSpan.FromSeconds(DefaultStatsTimeoutSeconds));

            try
            {
                _logger.LogTrace("Starting stats collection for container {ContainerId}", containerId);
                
                //// Start the stats stream (non-streaming, single snapshot)
                //var statsTask = _dockerClient.Containers.GetContainerStatsAsync(
                //    containerId,
                //    new ContainerStatsParameters { Stream = false, OneShot = true },
                //    progress,
                //    cts.Token);

                //// Wait for either the stats response or the task to complete
                //var completedTask = await Task.WhenAny(statsResponse.Task, statsTask);
                
                //// If the Docker task completed but we didn't get stats, something went wrong
                //if (completedTask == statsTask)
                //{
                //    await statsTask; // This will throw if there was an error
                    
                //    // If we get here, the task completed but no stats were received
                //    _logger.LogWarning("Docker stats task completed for container {ContainerId} but no stats were received", containerId);
                //    throw new InvalidOperationException($"No stats received for container {containerId}");
                //}

                await _dockerClient.Containers.GetContainerStatsAsync(
                    containerId,
                    new ContainerStatsParameters { Stream = false, OneShot = true },
                    progress,
                    default);

                // Get the stats (will be ready since statsResponse.Task completed)
                var stats = response;

                // Parse and return formatted stats
                var cpuDelta = (stats.CPUStats.CPUUsage.TotalUsage) - (stats.PreCPUStats.CPUUsage.TotalUsage);
                var systemDelta = (stats.CPUStats.SystemUsage ?? 0) - (stats.PreCPUStats.SystemUsage ?? 0);
                var cpuPercent = 0.0;

                if (systemDelta > 0 && cpuDelta > 0)
                {
                    cpuPercent = (double)cpuDelta / systemDelta * (stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1) * 100.0;
                }

                var memoryUsage = stats.MemoryStats.Usage ?? 0;
                var memoryLimit = stats.MemoryStats.Limit ?? 0;
                var memoryPercent = memoryLimit > 0 ? (double)memoryUsage / memoryLimit * 100.0 : 0.0;

                // Network I/O
                var networkRx = stats.Networks?.Values.Sum(n => (long)n.RxBytes) ?? 0;
                var networkTx = stats.Networks?.Values.Sum(n => (long)n.TxBytes) ?? 0;

                // Block I/O
                var blockRead = stats.BlkioStats?.IoServiceBytesRecursive?
                    .Where(io => io.Op == "read")
                    .Sum(io => (long)io.Value) ?? 0;
                var blockWrite = stats.BlkioStats?.IoServiceBytesRecursive?
                    .Where(io => io.Op == "write")
                    .Sum(io => (long)io.Value) ?? 0;

                _logger.LogDebug("Stats retrieved for container {ContainerId}: CPU {Cpu:F2}%, Memory {Memory:F2}%",
                    containerId, cpuPercent, memoryPercent);

                return new Models.ContainerStatsResponse
                {
                    ContainerId = containerId,
                    Timestamp = DateTime.UtcNow,
                    Cpu = new Models.CpuStats
                    {
                        UsagePercent = Math.Round(cpuPercent, 2),
                        TotalUsage = stats.CPUStats.CPUUsage.TotalUsage,
                        SystemUsage = stats.CPUStats.SystemUsage ?? 0,
                        OnlineCpus = stats.CPUStats.OnlineCPUs ?? 0
                    },
                    Memory = new Models.MemoryStats
                    {
                        UsageBytes = memoryUsage,
                        LimitBytes = memoryLimit,
                        UsagePercent = Math.Round(memoryPercent, 2),
                        MaxUsageBytes = stats.MemoryStats.MaxUsage ?? 0
                    },
                    Network = new Models.NetworkStats
                    {
                        RxBytes = networkRx,
                        TxBytes = networkTx
                    },
                    BlockIo = new Models.BlockIoStats
                    {
                        ReadBytes = blockRead,
                        WriteBytes = blockWrite
                    },
                    Pids = stats.PidsStats?.Current ?? 0
                };
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                _logger.LogWarning("Stats collection timed out for container {ContainerId} after 10 seconds", containerId);
                throw new TimeoutException($"Stats collection timed out for container {containerId}");
            }
        }

        public async IAsyncEnumerable<Models.ContainerStatsResponse> StreamContainerStatsAsync(
            string containerId, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("Starting stats stream for container {ContainerId} using Docker native streaming", containerId);

            // Create a channel to convert IProgress callbacks to IAsyncEnumerable
            var channel = System.Threading.Channels.Channel.CreateUnbounded<Models.ContainerStatsResponse>();

            // Start the Docker streaming in a background task
            // Important: Don't await this task - it runs until cancellation
            _ = Task.Run(async () =>
            {
                // Create progress handler inside the task to ensure proper context
                var progress = new Progress<DockerStatsResponse>(stats =>
                {
                    _logger.LogTrace("Received stats from Docker stream for container {ContainerId}", containerId);
                    
                    try
                    {
                        // Calculate CPU percentage
                        var cpuDelta = stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage;
                        var systemDelta = (stats.CPUStats.SystemUsage ?? 0) - (stats.PreCPUStats.SystemUsage ?? 0);
                        var cpuPercent = 0.0;

                        if (systemDelta > 0 && cpuDelta > 0)
                        {
                            cpuPercent = (double)cpuDelta / systemDelta * (stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1) * 100.0;
                        }

                        var memoryUsage = stats.MemoryStats.Usage ?? 0;
                        var memoryLimit = stats.MemoryStats.Limit ?? 0;
                        var memoryPercent = memoryLimit > 0 ? (double)memoryUsage / memoryLimit * 100.0 : 0.0;

                        // Network I/O
                        var networkRx = stats.Networks?.Values.Sum(n => (long)n.RxBytes) ?? 0;
                        var networkTx = stats.Networks?.Values.Sum(n => (long)n.TxBytes) ?? 0;

                        // Block I/O
                        var blockRead = stats.BlkioStats?.IoServiceBytesRecursive?
                            .Where(io => io.Op == "read")
                            .Sum(io => (long)io.Value) ?? 0;
                        var blockWrite = stats.BlkioStats?.IoServiceBytesRecursive?
                            .Where(io => io.Op == "write")
                            .Sum(io => (long)io.Value) ?? 0;

                        var response = new Models.ContainerStatsResponse
                        {
                            ContainerId = containerId,
                            Timestamp = DateTime.UtcNow,
                            Cpu = new Models.CpuStats
                            {
                                UsagePercent = Math.Round(cpuPercent, 2),
                                TotalUsage = stats.CPUStats.CPUUsage.TotalUsage,
                                SystemUsage = stats.CPUStats.SystemUsage ?? 0,
                                OnlineCpus = stats.CPUStats.OnlineCPUs ?? 0
                            },
                            Memory = new Models.MemoryStats
                            {
                                UsageBytes = memoryUsage,
                                LimitBytes = memoryLimit,
                                UsagePercent = Math.Round(memoryPercent, 2),
                                MaxUsageBytes = stats.MemoryStats.MaxUsage ?? 0
                            },
                            Network = new Models.NetworkStats
                            {
                                RxBytes = networkRx,
                                TxBytes = networkTx
                            },
                            BlockIo = new Models.BlockIoStats
                            {
                                ReadBytes = blockRead,
                                WriteBytes = blockWrite
                            },
                            Pids = stats.PidsStats?.Current ?? 0
                        };

                        // Write to channel (non-blocking)
                        channel.Writer.TryWrite(response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing stats in progress callback for container {ContainerId}", containerId);
                    }
                });

                try
                {
                    _logger.LogTrace("Starting Docker GetContainerStatsAsync with Stream=true for container {ContainerId}", containerId);
                    
                    // Stream = true: Docker will continuously push stats via IProgress callbacks
                    await _dockerClient.Containers.GetContainerStatsAsync(
                        containerId,
                        new ContainerStatsParameters 
                        { 
                            Stream = true,  // Enable continuous streaming
                            OneShot = false // Not a one-shot request
                        },
                        progress,
                        cancellationToken);
                    
                    _logger.LogTrace("Docker stats stream completed for container {ContainerId}", containerId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogTrace("Stats stream cancelled for container {ContainerId}", containerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Docker stats stream for container {ContainerId}", containerId);
                }
                finally
                {
                    channel.Writer.Complete();
                    _logger.LogTrace("Channel writer completed for container {ContainerId}", containerId);
                }
            }, cancellationToken);

            // Yield stats as they arrive from the channel
            // The stream task will continue running in the background until cancellation
            await foreach (var stats in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return stats;
            }

            _logger.LogTrace("Stats stream enumeration ended for container {ContainerId}", containerId);
            
            // Note: streamTask continues running until cancellation - this is intentional
            // The Docker stream is infinite, and we want to keep streaming as long as the consumer is reading
        }

        public async Task<Models.ContainerLogsResponse> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting logs for container {ContainerId}, tail={TailLines}", containerId, tailLines);

            var logsParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Timestamps = true,
                Tail = tailLines > 0 ? tailLines.ToString() : "100"
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            // Use the Enhanced API that returns a MultiplexedStream directly
            var logsStream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, logsParams, cts.Token);

            // The new API returns MultiplexedStream directly
            using var stdoutStream = new MemoryStream();
            using var stderrStream = new MemoryStream();

            // Use CopyOutputToAsync to demultiplex stdout and stderr
            await logsStream.CopyOutputToAsync(null, stdoutStream, stderrStream, cts.Token);

            // Convert streams to text
            var stdoutText = System.Text.Encoding.UTF8.GetString(stdoutStream.ToArray());
            var stderrText = System.Text.Encoding.UTF8.GetString(stderrStream.ToArray());

            // Combine and split into lines
            var logLines = new List<string>();

            if (!string.IsNullOrEmpty(stdoutText))
            {
                logLines.AddRange(stdoutText.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }

            if (!string.IsNullOrEmpty(stderrText))
            {
                logLines.AddRange(stderrText.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }

            _logger.LogDebug("Retrieved {Count} log lines for container {ContainerId}", logLines.Count, containerId);

            return new Models.ContainerLogsResponse
            {
                ContainerId = containerId,
                Timestamp = DateTime.UtcNow,
                LogLines = logLines.Count,
                Logs = logLines
            };
        }

        public async IAsyncEnumerable<string> StreamContainerLogsAsync(
            string containerId,
            bool follow = true,
            int tailLines = 100,
            bool timestamps = true,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting log stream for container {ContainerId} (follow={Follow}, tail={Tail})", 
                containerId, follow, tailLines);

            var logsParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = follow,
                Timestamps = timestamps,
                Tail = tailLines > 0 ? tailLines.ToString() : "all"
            };

            MultiplexedStream logsStream = null!;
            try
            {
                _logger.LogDebug("Calling Docker API to get container logs stream");
                
                // Get the multiplexed log stream from Docker
                logsStream = await _dockerClient.Containers.GetContainerLogsAsync(
                    containerId,
                    logsParams,
                    cancellationToken);

                _logger.LogDebug("Successfully got log stream from Docker, starting to read");

                // Use channels for async streaming
                var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

                // Background task to read from Docker stream and write to channel
                var readerTask = Task.Run(async () =>
                {
                    try
                    {
                        using var stdoutStream = new MemoryStream();
                        using var stderrStream = new MemoryStream();

                        // For streaming logs, we need to read continuously
                        var buffer = new byte[4096];
                        int readCount = 0;
                        
                        _logger.LogDebug("Starting read loop for container logs");
                        
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogTrace("Calling ReadOutputAsync, iteration {Count}", ++readCount);
                            
                            // Read from the multiplexed stream
                            var result = await logsStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
                            
                            _logger.LogTrace("ReadOutputAsync returned: EOF={EOF}, Count={Count}, Target={Target}", 
                                result.EOF, result.Count, result.Target);
                            
                            if (result.EOF)
                            {
                                _logger.LogDebug("Reached end of log stream for container {ContainerId} after {ReadCount} reads", 
                                    containerId, readCount);
                                break;
                            }

                            if (result.Count > 0)
                            {
                                _logger.LogDebug("Read {ByteCount} bytes from {Target}", result.Count, result.Target);
                                
                                // Convert bytes to string and write to channel
                                var logLine = result.Target == MultiplexedStream.TargetStream.StandardOut 
                                    ? $"[stdout] {System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count)}"
                                    : $"[stderr] {System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count)}";

                                // Split by newlines and send each line
                                var lines = logLine.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                _logger.LogDebug("Split into {LineCount} lines", lines.Length);
                                
                                foreach (var line in lines)
                                {
                                    _logger.LogTrace("Writing line to channel: {Line}", line.Length > 100 ? line.Substring(0, 100) + "..." : line);
                                    await channel.Writer.WriteAsync(line, cancellationToken);
                                }
                            }
                            else
                            {
                                _logger.LogTrace("ReadOutputAsync returned 0 bytes, continuing");
                            }

                            // Small delay to prevent tight loop
                            if (!follow)
                            {
                                await Task.Delay(10, cancellationToken);
                            }
                        }
                        
                        _logger.LogDebug("Exited read loop, total reads: {ReadCount}", readCount);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("Log stream cancelled for container {ContainerId}", containerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error streaming logs for container {ContainerId}", containerId);
                    }
                    finally
                    {
                        _logger.LogDebug("Completing channel writer");
                        channel.Writer.Complete();
                    }
                }, cancellationToken);

                _logger.LogDebug("Starting to yield lines from channel");
                
                // Yield log lines from channel
                int yieldCount = 0;
                await foreach (var logLine in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    _logger.LogTrace("Yielding line {Count}", ++yieldCount);
                    yield return logLine;
                }

                _logger.LogDebug("Yielded {Count} log lines total", yieldCount);
                await readerTask;
            }
            finally
            {
                logsStream?.Dispose();
                _logger.LogInformation("Log stream ended for container {ContainerId}", containerId);
            }
        }

        public async Task<Models.ContainerInspectResponse> InspectContainerAsync(string containerId, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Inspecting container {ContainerId}", containerId);

            var container = await _dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);

            return new Models.ContainerInspectResponse
            {
                ContainerId = containerId,
                Name = container.Name,
                State = new Models.ContainerState
                {
                    Status = container.State.Status,
                    Running = container.State.Running,
                    Paused = container.State.Paused,
                    Restarting = container.State.Restarting,
                    Pid = container.State.Pid,
                    StartedAt = DateTime.TryParse(container.State.StartedAt, out var startedAt) ? startedAt : DateTime.MinValue,
                    FinishedAt = DateTime.TryParse(container.State.FinishedAt, out var finishedAt) ? finishedAt : DateTime.MinValue
                },
                Created = container.Created,
                Image = container.Image,
                Platform = container.Platform
            };
        }

        public async Task<Models.ContainerListResponse> ListContainersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Listing containers on this node");

            var containers = await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = false // Only running containers
            }, cancellationToken);

            var nodeId = Environment.GetEnvironmentVariable("NODE_ID") ?? "unknown";

            return new Models.ContainerListResponse
            {
                NodeId = nodeId,
                Timestamp = DateTime.UtcNow,
                ContainerCount = containers.Count,
                Containers = containers.Select(c => new Models.ContainerSummary
                {
                    Id = c.ID,
                    Names = c.Names,
                    Image = c.Image,
                    State = c.State,
                    Status = c.Status
                }).ToList()
            };
        }
    }
}
