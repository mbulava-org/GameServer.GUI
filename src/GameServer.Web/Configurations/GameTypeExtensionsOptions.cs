namespace GameServer.Web.Configurations;

/// <summary>
/// Bound to the <c>GameTypeExtensions</c> configuration section. Restricts which
/// assemblies the <see cref="Services.V2.IGameTypeExtensionResolver"/> is allowed
/// to load extension components from.
/// </summary>
public sealed class GameTypeExtensionsOptions
{
    public const string SectionName = "GameTypeExtensions";

    /// <summary>
    /// Simple names (no version/culture/publickeytoken) of assemblies whose types are
    /// eligible for resolution. Defaults to a single-entry list containing
    /// <c>GameServer.Web</c>.
    /// </summary>
    public List<string> AllowedAssemblies { get; set; } = ["GameServer.Web"];
}
