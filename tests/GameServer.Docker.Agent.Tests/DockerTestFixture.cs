using Docker.DotNet;
using Docker.DotNet.Models;

namespace GameServer.Docker.Agent.Tests;

public class DockerTestFixture : IAsyncLifetime
{
    public IDockerClient DockerClient { get; private set; } = null!;
    public bool IsDockerAvailable { get; private set; }
    public string? TestContainerId { get; private set; }
    public const string TestImage = "alpine:latest";

    public async ValueTask InitializeAsync()
    {
        try
        {
            var dockerUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ??
                (OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock");

            DockerClient = new DockerClientBuilder().WithEndpoint(new Uri(dockerUri)).Build();
            await DockerClient.System.GetVersionAsync();
            IsDockerAvailable = true;

            // Pull image if not already present
            try
            {
                await DockerClient.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = "alpine", Tag = "latest" },
                    new AuthConfig(),
                    new Progress<JSONMessage>());
            }
            catch
            {
                // Image might already exist or network might be offline
            }

            // Create a lightweight running container for tests
            var container = await DockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = TestImage,
                Cmd = ["sh", "-c", "echo 'ready'; while true; do sleep 1; done"],
                Name = $"gameserver-agent-test-{Guid.NewGuid():N}",
                Tty = true,
                AttachStdout = true,
                AttachStderr = true
            });

            TestContainerId = container.ID;
            await DockerClient.Containers.StartContainerAsync(TestContainerId, new ContainerStartParameters());
        }
        catch (Exception)
        {
            IsDockerAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDockerAvailable && TestContainerId != null)
        {
            try
            {
                await DockerClient.Containers.StopContainerAsync(TestContainerId, new ContainerStopParameters { WaitBeforeKillSeconds = 1 });
            }
            catch { }

            try
            {
                await DockerClient.Containers.RemoveContainerAsync(TestContainerId, new ContainerRemoveParameters { Force = true });
            }
            catch { }
        }

        DockerClient?.Dispose();
    }
}

[CollectionDefinition("Docker Functional Tests")]
public class DockerTestCollection : ICollectionFixture<DockerTestFixture>
{
}
