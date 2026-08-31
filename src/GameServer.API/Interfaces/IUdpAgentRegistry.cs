using GameServer.API.Models;

namespace GameServer.API.Interfaces
{
    /// <summary>
    /// Stores agents discovered through UDP announcements.
    /// </summary>
    public interface IUdpAgentRegistry
    {
        void UpsertAnnouncement(UdpAgentAnnouncement announcement);

        NodeAgentEndpoint? GetAgentForContainer(string containerId);

        IReadOnlyCollection<NodeAgentEndpoint> GetAllAgents();

        void RemoveExpired(DateTimeOffset now);
    }
}
