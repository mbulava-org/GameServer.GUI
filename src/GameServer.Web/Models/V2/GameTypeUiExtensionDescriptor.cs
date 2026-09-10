namespace GameServer.Web.Models.V2;

/// <summary>
/// GUI-side view of a UI extension descriptor attached to a <c>GameTypeRevision</c>.
/// Declares which Blazor component should be attached to the server details page as
/// an additional tab. The resolver in <c>GameServer.Web</c> enforces an assembly
/// whitelist before actually rendering these.
/// </summary>
public sealed record GameTypeUiExtensionDescriptor
{
    /// <summary>
    /// Fully-qualified type name of the Blazor component to render (e.g.
    /// <c>GameServer.Web.Components.Server.Extensions.PalworldApiTab</c>).
    /// </summary>
    public string ComponentTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Optional assembly the component lives in. Must appear in the appsettings
    /// whitelist (<c>GameTypeExtensions:AllowedAssemblies</c>). When omitted, the
    /// resolver only searches whitelisted assemblies.
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>User-facing tab title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional Radzen icon name.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Tab ordering relative to other extension tabs (built-in tabs always render first).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Optional string parameters forwarded to the component via
    /// <c>[Parameter] IReadOnlyDictionary&lt;string,string&gt; ExtensionParameters</c>.
    /// </summary>
    public Dictionary<string, string> Parameters { get; init; } = new();
}
