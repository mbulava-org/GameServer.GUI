using Docker.DotNet;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using DockerModels = global::Docker.DotNet.Models;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Docker image operations controller.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ImagesController : ControllerBase
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<ImagesController> _logger;

        public ImagesController(IDockerClient dockerClient, ILogger<ImagesController> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        /// <summary>
        /// Inspect a Docker image available on this node.
        /// </summary>
        [HttpPost("inspect")]
        [ProducesResponseType(200, Type = typeof(GameServer.Docker.Agent.Models.ImageInspectResponse))]
        [ProducesResponseType(400, Type = typeof(ErrorResponse))]
        [ProducesResponseType(404, Type = typeof(ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> InspectImage([FromBody] InspectImageRequest request, CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.ImageReference))
            {
                return BadRequest(new ErrorResponse { Error = "Image reference is required." });
            }

            try
            {
                _logger.LogInformation("Inspecting image {ImageReference}", request.ImageReference);

                var image = await InspectImageAsync(request, cancellationToken);

                return Ok(new GameServer.Docker.Agent.Models.ImageInspectResponse
                {
                    ImageReference = request.ImageReference,
                    RepoDigests = image.RepoDigests?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    EnvironmentVariables = image.Config?.Env?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    ExposedPorts = image.Config?.ExposedPorts?.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    VolumePaths = image.Config?.Volumes?.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? []
                });
            }
            catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Image {ImageReference} was not found on this node", request.ImageReference);
                return NotFound(new ErrorResponse { Error = $"Image '{request.ImageReference}' was not found on this node." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inspecting image {ImageReference}", request.ImageReference);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        private async Task<DockerModels.ImageInspectResponse> InspectImageAsync(InspectImageRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await _dockerClient.Images.InspectImageAsync(request.ImageReference, cancellationToken);
            }
            catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound && request.PullIfMissing)
            {
                _logger.LogInformation("Image {ImageReference} was not found locally. Pulling before retrying inspection.", request.ImageReference);

                await PullImageAsync(request.ImageReference, cancellationToken);

                return await _dockerClient.Images.InspectImageAsync(request.ImageReference, cancellationToken);
            }
        }

        private async Task PullImageAsync(string imageReference, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imageReference);

            var (repository, tag) = ParseImageReference(imageReference);
            var parameters = new DockerModels.ImagesCreateParameters
            {
                FromImage = repository,
                Tag = tag
            };

            var progress = new Progress<DockerModels.JSONMessage>(message =>
            {
                if (message.Error is not null && !string.IsNullOrWhiteSpace(message.Error.Message))
                {
                    _logger.LogWarning("Image pull warning for {ImageReference}: {Message}", imageReference, message.Error.Message);
                }
            });

            await _dockerClient.Images.CreateImageAsync(parameters, null, progress, cancellationToken);
            _logger.LogInformation("Pulled image {ImageReference}", imageReference);
        }

        private static (string Repository, string Tag) ParseImageReference(string imageReference)
        {
            var digestIndex = imageReference.IndexOf('@', StringComparison.Ordinal);
            if (digestIndex >= 0)
            {
                return (imageReference, string.Empty);
            }

            var separatorIndex = imageReference.LastIndexOf(':');
            var slashIndex = imageReference.LastIndexOf('/');
            return separatorIndex > slashIndex && separatorIndex < imageReference.Length - 1
                ? (imageReference[..separatorIndex], imageReference[(separatorIndex + 1)..])
                : (imageReference, "latest");
        }
    }
}
