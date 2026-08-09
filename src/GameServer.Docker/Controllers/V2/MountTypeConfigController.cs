using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers.V2;

/// <summary>
/// Provides read/write access to mount-type configuration templates.
/// </summary>
[ApiController]
[Route("api/v2/mounttypeconfigs")]
public sealed class MountTypeConfigController : ControllerBase
{
    private readonly IMountTypeConfigRepository repository;
    private readonly ILogger<MountTypeConfigController> logger;

    public MountTypeConfigController(
        IMountTypeConfigRepository repository,
        ILogger<MountTypeConfigController> logger)
    {
        this.repository = repository;
        this.logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IReadOnlyList<MountTypeConfigDto>))]
    public async Task<ActionResult<IReadOnlyList<MountTypeConfigDto>>> GetAll(CancellationToken cancellationToken)
    {
        var configs = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(configs.Select(MapToDto).ToList());
    }

    [HttpGet("{key}")]
    [ProducesResponseType(200, Type = typeof(MountTypeConfigDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<MountTypeConfigDto>> Get(string key, CancellationToken cancellationToken)
    {
        var config = await repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
        if (config is null)
        {
            return NotFound();
        }

        return Ok(MapToDto(config));
    }

    [HttpPut("{key}")]
    [ProducesResponseType(200, Type = typeof(MountTypeConfigDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<MountTypeConfigDto>> Save(
        string key,
        [FromBody] MountTypeConfigDto dto,
        CancellationToken cancellationToken)
    {
        if (dto is null)
        {
            return BadRequest("A configuration payload is required.");
        }

        if (!string.Equals(key, dto.Key, StringComparison.Ordinal))
        {
            return BadRequest("Key in route must match key in body.");
        }

        var model = new MountTypeConfig
        {
            Key = dto.Key,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            VolumeNameFormat = dto.VolumeNameFormat,
            Options = dto.Options,
            IsActive = dto.IsActive
        };

        var saved = await repository.SaveAsync(model, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Mount type configuration '{Key}' saved.", saved.Key);
        return Ok(MapToDto(saved));
    }

    [HttpDelete("{key}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Mount type configuration '{Key}' deleted.", key);
        return NoContent();
    }

    private static MountTypeConfigDto MapToDto(MountTypeConfig config)
    {
        return new MountTypeConfigDto
        {
            Key = config.Key,
            DisplayName = config.DisplayName,
            Description = config.Description,
            VolumeNameFormat = config.VolumeNameFormat,
            Options = config.Options,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }
}
