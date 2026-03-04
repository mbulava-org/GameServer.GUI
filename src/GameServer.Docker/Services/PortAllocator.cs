using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using GameServer.Docker.Configurations;
using Microsoft.Extensions.Options;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Allocates ports for game servers by querying Docker Swarm for already-used ports.
    /// NOTE: Only works in Direct mode where IDockerClient is available.
    /// In Agent mode, port allocation should be handled differently.
    /// </summary>
    public class PortAllocator
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly PortAllocation _portOptions;
        private readonly IDockerClient? client;

        public PortAllocator(
            IDockerClient? client,
            IOptions<PortAllocation> portOptions)
        {
            _portOptions = portOptions.Value;
            this.client = client;
        }


        /// <summary>
        /// Checks if the given port and protocol is available for allocation
        /// </summary>
        /// <param name="port">The Port Number to be checked</param>
        /// <param name="protocol">tpc or udp</param>
        /// <returns>true if the combination is not currently exposed.</returns>
        public async Task<bool> IsProtocolPortAvailable(uint port, string protocol = "tcp")
        {
            if (port < 1024)
                return false;

            if (port < _portOptions.StartPort || port > _portOptions.EndPort)
                return false;

            protocol = (protocol ?? "tcp").ToLowerInvariant();

            if (client == null)
            {
                throw new InvalidOperationException(
                    "PortAllocator requires IDockerClient which is not available in Agent mode. " +
                    "Port allocation in Agent mode needs a different implementation.");
            }

            // Check Docker Swarm services for the same published port + protocol
            var services = await client.Swarm.ListServicesAsync();
            foreach (var svc in services)
            {
                //If nothing is published, skip
                if (svc.Endpoint?.Ports == null || !svc.Endpoint.Ports.Any()) continue;
                // Check each published port & Protocol 
                foreach (var p in svc.Endpoint.Ports)
                {
                    if (p.PublishedPort == port && p.Protocol.ToLowerInvariant() == protocol)
                    {
                        return false;
                    }
                }

            }

            return true;
        }
    }

}
