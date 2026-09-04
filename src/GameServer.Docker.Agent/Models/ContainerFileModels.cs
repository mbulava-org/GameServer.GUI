namespace GameServer.Docker.Agent.Models
{
    public sealed record ContainerFileItemResponse
    {
        public required string Name { get; init; }

        public required string Path { get; init; }

        public long Size { get; init; }

        public bool IsDirectory { get; init; }

        public DateTime LastModified { get; init; }

        public string? Extension { get; init; }

        public string? Permissions { get; init; }
    }

    public sealed record SaveFileRequest
    {
        public string Content { get; init; } = string.Empty;
    }
}
