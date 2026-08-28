using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.API.Configurations;
using GameServer.API.Interfaces;
// (Removed unused using GameServer.API.Extensions)

namespace GameServer.API.Services
{
    /// <summary>
    /// Allocates ports for game servers by querying Docker Swarm for already-used ports.
    /// </summary>
    public class PortAllocator
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly PortAllocation _portOptions;
        private readonly IServiceOperations _serviceOperations;

        public PortAllocator(
            IServiceOperations secretsOperations,
            PortAllocation portOptions)
        {
            _portOptions = portOptions;
            _serviceOperations = secretsOperations;
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

            // Check if the port is reserved via configuration
            if (IsReserved(port))
                return false;

            protocol = (protocol ?? "tcp").ToLowerInvariant();


            // Check Docker Swarm services for the same published port + protocol
            var services = await _serviceOperations.ListServicesAsync();
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
        private bool IsReserved(uint port)
        {
            

            if (_portOptions.ReservedPortRanges == null)
                return false;

            foreach (var range in _portOptions.ReservedPortRanges)
            {
                if (string.IsNullOrWhiteSpace(range))
                    continue;
                if (range.Contains("-"))
                {
                    var parts = range.Split('-');
                    if (parts.Length != 2) continue;
                    if (uint.TryParse(parts[0].Trim(), out var start) && uint.TryParse(parts[1].Trim(), out var end))
                    {
                        if (start <= port && port <= end)
                            return true;
                    }
                }
                else if (uint.TryParse(range.Trim(), out var single))
                {
                    if (single == port)
                        return true;
                }
            }
            return false;
        }
    }

}
