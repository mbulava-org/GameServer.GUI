using GameServer.API.Client.Interfaces;
using GameServer.API.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Reflection;

namespace GameServer.API.Client.Extensions
{
    /// <summary>
    /// Extension methods for registering GameServer.API client services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds container console client services to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/console")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerConsoleClient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddSingleton<IContainerConsoleClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ContainerConsoleClient>>();
                return new ContainerConsoleClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds container console client services with custom hub connection configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <param name="configureConnection">Action to configure the hub connection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerConsoleClient(
            this IServiceCollection services,
            string hubUrl,
            Action<IHubConnectionBuilder> configureConnection)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            if (configureConnection == null)
                throw new ArgumentNullException(nameof(configureConnection));

            services.AddSingleton<IContainerConsoleClient>(sp =>
            {
                var builder = new HubConnectionBuilder()
                    .WithUrl(hubUrl);

                // Apply custom configuration
                configureConnection(builder);

                var connection = builder.Build();
                var logger = sp.GetService<ILogger<ContainerConsoleClient>>();
                
                return new ContainerConsoleClient(connection, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds container console client as transient (creates new instance per request)
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerConsoleClientTransient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddTransient<IContainerConsoleClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ContainerConsoleClient>>();
                return new ContainerConsoleClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds container terminal client services to the service collection for
        /// per-user interactive exec sessions.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/terminal")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerTerminalClient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddSingleton<IContainerTerminalClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ContainerTerminalClient>>();
                return new ContainerTerminalClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds container terminal client services with custom hub connection configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <param name="configureConnection">Action to configure the hub connection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerTerminalClient(
            this IServiceCollection services,
            string hubUrl,
            Action<IHubConnectionBuilder> configureConnection)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            if (configureConnection == null)
                throw new ArgumentNullException(nameof(configureConnection));

            services.AddSingleton<IContainerTerminalClient>(sp =>
            {
                var builder = new HubConnectionBuilder()
                    .WithUrl(hubUrl);

                configureConnection(builder);

                var connection = builder.Build();
                var logger = sp.GetService<ILogger<ContainerTerminalClient>>();

                return new ContainerTerminalClient(connection, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds container terminal client as transient (creates new instance per request)
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddContainerTerminalClientTransient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddTransient<IContainerTerminalClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ContainerTerminalClient>>();
                return new ContainerTerminalClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds resource monitoring client services to the service collection
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL (e.g., "https://your-server/hubs/resources")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddResourceMonitoringClient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddSingleton<IResourceMonitoringClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ResourceMonitoringClient>>();
                return new ResourceMonitoringClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds resource monitoring client services with custom hub connection configuration
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <param name="configureConnection">Action to configure the hub connection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddResourceMonitoringClient(
            this IServiceCollection services,
            string hubUrl,
            Action<IHubConnectionBuilder> configureConnection)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            if (configureConnection == null)
                throw new ArgumentNullException(nameof(configureConnection));

            services.AddSingleton<IResourceMonitoringClient>(sp =>
            {
                var builder = new HubConnectionBuilder()
                    .WithUrl(hubUrl);

                configureConnection(builder);

                var connection = builder.Build();
                var logger = sp.GetService<ILogger<ResourceMonitoringClient>>();
                
                return new ResourceMonitoringClient(connection, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds resource monitoring client as transient (creates new instance per request)
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="hubUrl">SignalR hub URL</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddResourceMonitoringClientTransient(
            this IServiceCollection services,
            string hubUrl)
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                throw new ArgumentException("Hub URL cannot be null or empty", nameof(hubUrl));

            services.AddTransient<IResourceMonitoringClient>(sp =>
            {
                var logger = sp.GetService<ILogger<ResourceMonitoringClient>>();
                return new ResourceMonitoringClient(hubUrl, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds all GameServer.API REST API clients (auto-generated from OpenAPI) to the service collection.
        /// Requires an HttpClient factory to be registered.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="baseUrl">Base URL of the GameServer.API API (e.g., "https://your-api.com")</param>
        /// <returns>Service collection for chaining</returns>
        /// <summary>
        /// Adds the auto-generated REST API clients (from the current OpenAPI document) to the service collection.
        /// Because the generated namespace matches the compiled assembly, clients are discovered at build time.
        /// </summary>
        public static IServiceCollection AddGameServerApiClients(
            this IServiceCollection services,
            string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));

            // Ensure HttpClient factory is available
            services.AddHttpClient();

            // Clients are generated by NSwag from the OpenAPI document. Update this list whenever the
            // controller surface changes so regeneration stays in sync with registration.
            services.AddHttpClient<IGameServersApi, GameServersApi>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(100);
            });

            services.AddHttpClient<IGameTypesApi, GameTypesApi>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(100);
            });

            services.AddHttpClient<IPortApi, PortApi>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(100);
            });

            return services;
        }

        /// <summary>
        /// Adds all GameServer.API client services (both REST API and SignalR) to the service collection.
        /// This is a convenience method that registers everything at once.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="apiBaseUrl">Base URL of the REST API (e.g., "https://your-api.com")</param>
        /// <param name="consoleHubUrl">URL of the Attach/Console SignalR hub (e.g., "https://your-api.com/hubs/attach")</param>
        /// <param name="resourcesHubUrl">URL of the Resources SignalR hub (e.g., "https://your-api.com/hubs/resources")</param>
        /// <param name="terminalHubUrl">URL of the Terminal SignalR hub (e.g., "https://your-api.com/hubs/terminal")</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddGameServerClients(
            this IServiceCollection services,
            string apiBaseUrl,
            string? consoleHubUrl = null,
            string? resourcesHubUrl = null,
            string? terminalHubUrl = null)
        {
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                throw new ArgumentException("API base URL cannot be null or empty", nameof(apiBaseUrl));

            // Register REST API clients
            services.AddGameServerApiClients(apiBaseUrl);

            // Register SignalR clients if URLs provided
            if (!string.IsNullOrWhiteSpace(consoleHubUrl))
            {
                services.AddContainerConsoleClient(consoleHubUrl);
            }

            if (!string.IsNullOrWhiteSpace(resourcesHubUrl))
            {
                services.AddResourceMonitoringClient(resourcesHubUrl);
            }

            if (!string.IsNullOrWhiteSpace(terminalHubUrl))
            {
                services.AddContainerTerminalClient(terminalHubUrl);
            }

            return services;
        }

        /// <summary>
        /// Adds all GameServer.API client services with custom configuration.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="apiBaseUrl">Base URL of the REST API</param>
        /// <param name="configureHttpClient">Action to configure the HttpClient for REST API calls</param>
        /// <param name="consoleHubUrl">URL of the Attach/Console SignalR hub</param>
        /// <param name="configureConsoleHub">Action to configure the Attach hub connection</param>
        /// <param name="resourcesHubUrl">URL of the Resources SignalR hub</param>
        /// <param name="configureResourcesHub">Action to configure the Resources hub connection</param>
        /// <param name="terminalHubUrl">URL of the Terminal SignalR hub</param>
        /// <param name="configureTerminalHub">Action to configure the Terminal hub connection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddGameServerClients(
            this IServiceCollection services,
            string apiBaseUrl,
            Action<HttpClient>? configureHttpClient = null,
            string? consoleHubUrl = null,
            Action<IHubConnectionBuilder>? configureConsoleHub = null,
            string? resourcesHubUrl = null,
            Action<IHubConnectionBuilder>? configureResourcesHub = null,
            string? terminalHubUrl = null,
            Action<IHubConnectionBuilder>? configureTerminalHub = null)
        {
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                throw new ArgumentException("API base URL cannot be null or empty", nameof(apiBaseUrl));

            // Register HttpClient for REST API
            services.AddHttpClient();
            services.AddHttpClient("GameServer.API.Api", client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(100);
                configureHttpClient?.Invoke(client);
            });

            // Register SignalR clients with custom configuration
            if (!string.IsNullOrWhiteSpace(consoleHubUrl))
            {
                if (configureConsoleHub != null)
                {
                    services.AddContainerConsoleClient(consoleHubUrl, configureConsoleHub);
                }
                else
                {
                    services.AddContainerConsoleClient(consoleHubUrl);
                }
            }

            if (!string.IsNullOrWhiteSpace(resourcesHubUrl))
            {
                if (configureResourcesHub != null)
                {
                    services.AddResourceMonitoringClient(resourcesHubUrl, configureResourcesHub);
                }
                else
                {
                    services.AddResourceMonitoringClient(resourcesHubUrl);
                }
            }

            if (!string.IsNullOrWhiteSpace(terminalHubUrl))
            {
                if (configureTerminalHub != null)
                {
                    services.AddContainerTerminalClient(terminalHubUrl, configureTerminalHub);
                }
                else
                {
                    services.AddContainerTerminalClient(terminalHubUrl);
                }
            }

            return services;
        }
    }
}
