namespace GameServer.Web.Services;

public interface IPublicIpService
{
    Task<string> GetPublicIpAsync(CancellationToken cancellationToken = default);
}
