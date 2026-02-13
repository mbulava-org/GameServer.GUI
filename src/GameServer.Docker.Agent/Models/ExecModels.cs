namespace GameServer.Docker.Agent.Models
{
    /// <summary>
    /// Request to execute a command in a container
    /// </summary>
    public class ExecRequest
    {
        /// <summary>
        /// Command and arguments to execute
        /// </summary>
        public string[]? Cmd { get; set; }

        /// <summary>
        /// Attach to stdout
        /// </summary>
        public bool AttachStdout { get; set; } = true;

        /// <summary>
        /// Attach to stderr
        /// </summary>
        public bool AttachStderr { get; set; } = true;
    }

    /// <summary>
    /// Response from command execution
    /// </summary>
    public class ExecResponse
    {
        /// <summary>
        /// Exit code of the command
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Combined stdout/stderr output
        /// </summary>
        public string Output { get; set; } = string.Empty;
    }
}
