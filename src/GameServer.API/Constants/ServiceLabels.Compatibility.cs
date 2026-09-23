namespace GameServer.API.Constants;

/// <summary>
/// Backward-compatible API namespace for shared Docker service label constants.
/// </summary>
public static class ServiceLabels
{
    public const string Managed = GameServer.Docker.Constants.ServiceLabels.Managed;
    public const string ServerId = GameServer.Docker.Constants.ServiceLabels.ServerId;
    public const string Name = GameServer.Docker.Constants.ServiceLabels.Name;
    public const string Description = GameServer.Docker.Constants.ServiceLabels.Description;
    public const string GameType = GameServer.Docker.Constants.ServiceLabels.GameType;
    public const string ManagedValue = GameServer.Docker.Constants.ServiceLabels.ManagedValue;
}
