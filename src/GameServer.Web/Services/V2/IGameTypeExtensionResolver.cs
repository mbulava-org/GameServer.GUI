using GameServer.Web.Components.Server.Extensions;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

/// <summary>
/// Result of resolving a single <see cref="GameTypeUiExtensionDescriptor"/> against
/// the assembly whitelist. When <see cref="ComponentType"/> is <c>null</c>,
/// <see cref="FailureReason"/> explains why and the caller should render the
/// fallback <c>NotYetImplementedTab</c>.
/// </summary>
public sealed record GameTypeExtensionResolution
{
    public required GameTypeUiExtensionDescriptor Descriptor { get; init; }

    public Type? ComponentType { get; init; }

    public string? FailureReason { get; init; }

    public bool IsResolved => ComponentType is not null;
}

public interface IGameTypeExtensionResolver
{
    /// <summary>
    /// Resolves the ordered set of extension tabs for the given descriptor list.
    /// Always returns one entry per descriptor. Ordering is stable by
    /// <see cref="GameTypeUiExtensionDescriptor.Order"/> then <see cref="GameTypeUiExtensionDescriptor.Title"/>.
    /// </summary>
    IReadOnlyList<GameTypeExtensionResolution> Resolve(IEnumerable<GameTypeUiExtensionDescriptor>? descriptors);
}
