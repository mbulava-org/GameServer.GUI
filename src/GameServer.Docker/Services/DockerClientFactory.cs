using Docker.DotNet;
using Microsoft.Extensions.Options;

namespace GameServer.Docker.Services
{
    public class DockerClientFactory(ILogger<DockerClientFactory> logger, IOptions<Configurations.DockerConnection> dockerConnectionOptions)
    {
        public DockerClient Create()
        {
            logger.LogInformation("Creating DockerClient Instance.");
            return new DockerClientConfiguration(
                new Uri(dockerConnectionOptions.Value.Uri)
            ).CreateClient();
        }
    }

}
