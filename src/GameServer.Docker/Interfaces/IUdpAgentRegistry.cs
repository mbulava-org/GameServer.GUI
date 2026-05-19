using GameServer.Docker.Models;

namespace GameServer.Docker.Interfaces
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
