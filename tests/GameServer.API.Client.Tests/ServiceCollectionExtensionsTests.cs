using GameServer.API.Client.Extensions;
using GameServer.API.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.API.Client.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddGameServerClients_RegistersAllClientsAndApis()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddGameServerClients(
                apiBaseUrl: "http://localhost:5164",
                consoleHubUrl: "http://localhost:5164/hubs/attach",
                resourcesHubUrl: "http://localhost:5164/hubs/resources",
                terminalHubUrl: "http://localhost:5164/hubs/terminal");

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IGameServersApi>());
            Assert.NotNull(provider.GetService<IGameTypesApi>());
            Assert.NotNull(provider.GetService<IPortApi>());
            Assert.NotNull(provider.GetService<IContainerConsoleClient>());
            Assert.NotNull(provider.GetService<IResourceMonitoringClient>());
            Assert.NotNull(provider.GetService<IContainerTerminalClient>());
        }

        [Fact]
        public void AddGameServerClients_WithCustomConfigurations_RegistersSuccessfully()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddGameServerClients(
                apiBaseUrl: "http://localhost:5164",
                configureHttpClient: client => client.Timeout = TimeSpan.FromSeconds(30),
                consoleHubUrl: "http://localhost:5164/hubs/attach",
                configureConsoleHub: _ => { },
                resourcesHubUrl: "http://localhost:5164/hubs/resources",
                configureResourcesHub: _ => { },
                terminalHubUrl: "http://localhost:5164/hubs/terminal",
                configureTerminalHub: _ => { });

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IContainerConsoleClient>());
            Assert.NotNull(provider.GetService<IResourceMonitoringClient>());
            Assert.NotNull(provider.GetService<IContainerTerminalClient>());
        }

        [Fact]
        public void AddTransientClients_RegistersTransientInstances()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddContainerConsoleClientTransient("http://localhost:5164/hubs/attach");
            services.AddResourceMonitoringClientTransient("http://localhost:5164/hubs/resources");
            services.AddContainerTerminalClientTransient("http://localhost:5164/hubs/terminal");

            var provider = services.BuildServiceProvider();

            var c1 = provider.GetService<IContainerConsoleClient>();
            var c2 = provider.GetService<IContainerConsoleClient>();
            Assert.NotNull(c1);
            Assert.NotNull(c2);
            Assert.NotSame(c1, c2);
        }

        [Fact]
        public void AddGameServerClients_InvalidUrls_ThrowsArgumentException()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentException>(() => services.AddGameServerClients("", (string?)null, (string?)null, (string?)null));
            Assert.Throws<ArgumentException>(() => services.AddContainerConsoleClient(""));
            Assert.Throws<ArgumentException>(() => services.AddResourceMonitoringClient(""));
            Assert.Throws<ArgumentException>(() => services.AddContainerTerminalClient(""));
        }
    }
}
