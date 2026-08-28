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
        [ProducesResponseType(408, Type = typeof(ErrorResponse))]
        [ProducesResponseType(500)]
        public async Task<IActionResult> InspectImage([FromBody] InspectImageRequest request, CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.ImageReference))
            {
                return BadRequest(new ErrorResponse { Error = "Image reference is required." });
            }

            var sanitizedReference = SanitizeImageReference(request.ImageReference);
            if (string.IsNullOrWhiteSpace(sanitizedReference))
            {
                return BadRequest(new ErrorResponse { Error = "Invalid image reference provided." });
            }

            try
            {
                _logger.LogInformation("Inspecting image {ImageReference} (Sanitized: {Sanitized})", request.ImageReference, sanitizedReference);

                var image = await InspectImageAsync(sanitizedReference, request.PullIfMissing, cancellationToken);

                return Ok(new GameServer.Docker.Agent.Models.ImageInspectResponse
                {
                    ImageReference = sanitizedReference,
                    RepoDigests = image.RepoDigests?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    EnvironmentVariables = image.Config?.Env?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    ExposedPorts = image.Config?.ExposedPorts?.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [],
                    VolumePaths = image.Config?.Volumes?.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? []
                });
            }
            catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Image {ImageReference} was not found on this node", sanitizedReference);
                return NotFound(new ErrorResponse { Error = $"Image '{sanitizedReference}' was not found on this node." });
            }
            catch (DockerApiException ex)
            {
                _logger.LogWarning(ex, "Docker API error inspecting image {ImageReference}: {Message}", sanitizedReference, ex.Message);
                return StatusCode((int)ex.StatusCode, new ErrorResponse { Error = ex.Message });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Inspection of image {ImageReference} timed out or was cancelled", sanitizedReference);
                return StatusCode(408, new ErrorResponse { Error = $"Inspection or pull of image '{sanitizedReference}' timed out or was cancelled." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inspecting image {ImageReference}", sanitizedReference);
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        private async Task<DockerModels.ImageInspectResponse> InspectImageAsync(string imageReference, bool pullIfMissing, CancellationToken cancellationToken)
        {
            try
            {
                return await _dockerClient.Images.InspectImageAsync(imageReference, cancellationToken);
            }
            catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound && pullIfMissing)
            {
                _logger.LogInformation("Image {ImageReference} was not found locally. Pulling before retrying inspection.", imageReference);

                await PullImageAsync(imageReference, cancellationToken);

                return await _dockerClient.Images.InspectImageAsync(imageReference, cancellationToken);
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

        public static string SanitizeImageReference(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
            {
                return string.Empty;
            }

            var cleaned = imageReference.Trim();

            // Strip fragment (#...) and query parameters (?...)
            var fragmentIndex = cleaned.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                cleaned = cleaned[..fragmentIndex].Trim();
            }

            var queryIndex = cleaned.IndexOf('?');
            if (queryIndex >= 0)
            {
                cleaned = cleaned[..queryIndex].Trim();
            }

            // Strip HTTP/HTTPS protocols
            if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[7..].Trim();
            }
            else if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[8..].Trim();
            }

            // Handle Docker Hub web URLs
            if (cleaned.StartsWith("hub.docker.com/r/", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[17..].Trim();
            }
            else if (cleaned.StartsWith("hub.docker.com/_/", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[17..].Trim();
            }
            else if (cleaned.StartsWith("hub.docker.com/", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[15..].Trim();
            }
            else if (cleaned.StartsWith("docker.io/library/", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[18..].Trim();
            }

            // Remove any leading/trailing slashes
            return cleaned.Trim('/');
        }

        private static (string Repository, string Tag) ParseImageReference(string imageReference)
        {
            var sanitized = SanitizeImageReference(imageReference);

            var digestIndex = sanitized.IndexOf('@', StringComparison.Ordinal);
            if (digestIndex >= 0)
            {
                return (sanitized, string.Empty);
            }

            var separatorIndex = sanitized.LastIndexOf(':');
            var slashIndex = sanitized.LastIndexOf('/');
            return separatorIndex > slashIndex && separatorIndex < sanitized.Length - 1
                ? (sanitized[..separatorIndex], sanitized[(separatorIndex + 1)..])
                : (sanitized, "latest");
        }
    }
}
