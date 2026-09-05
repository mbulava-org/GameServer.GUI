namespace GameServer.Web.Components.Server.Extensions;

/// <summary>
/// Marker interface for Blazor components that can be attached to a server details
/// page as a UI extension tab, resolved from a <c>GameTypeRevision.UiExtensionsJson</c>
/// descriptor. Intentionally empty for v1; reserved for future capability negotiation
/// (permissions, disposal, etc.). The resolver requires implementations to be Razor
/// components and to sit in a whitelisted assembly.
/// </summary>
public interface IGameTypeExtensionComponent
{
}
