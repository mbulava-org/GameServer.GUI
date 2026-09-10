using GameServer.Web.Components.Server.Extensions;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Services.V2;

/// <summary>
/// Resolves UI extension descriptors to concrete Blazor component types, enforcing
/// the appsettings assembly whitelist. Only inspects assemblies already loaded into
/// the current <see cref="AppDomain"/>; never triggers assembly loading based on
/// descriptor data.
/// </summary>
public sealed class GameTypeExtensionResolver : IGameTypeExtensionResolver
{
    private readonly IOptionsMonitor<GameTypeExtensionsOptions> _options;
    private readonly ILogger<GameTypeExtensionResolver> _logger;

    public GameTypeExtensionResolver(
        IOptionsMonitor<GameTypeExtensionsOptions> options,
        ILogger<GameTypeExtensionResolver> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IReadOnlyList<GameTypeExtensionResolution> Resolve(IEnumerable<GameTypeUiExtensionDescriptor>? descriptors)
    {
        if (descriptors is null)
        {
            return [];
        }

        var allowedAssemblies = new HashSet<string>(
            _options.CurrentValue.AllowedAssemblies ?? [],
            StringComparer.OrdinalIgnoreCase);

        return descriptors
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .Select(d => ResolveOne(d, allowedAssemblies))
            .ToList();
    }

    private GameTypeExtensionResolution ResolveOne(GameTypeUiExtensionDescriptor descriptor, HashSet<string> allowedAssemblies)
    {
        if (string.IsNullOrWhiteSpace(descriptor.ComponentTypeName))
        {
            return Fail(descriptor, "Descriptor is missing ComponentTypeName.");
        }

        if (allowedAssemblies.Count == 0)
        {
            return Fail(descriptor, "No assemblies are configured in GameTypeExtensions:AllowedAssemblies.");
        }

        if (descriptor.AssemblyName is { Length: > 0 } asm && !allowedAssemblies.Contains(asm))
        {
            return Fail(descriptor, $"Assembly '{asm}' is not in the GameTypeExtensions:AllowedAssemblies whitelist.");
        }

        var candidates = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name is not null && allowedAssemblies.Contains(name);
            })
            .ToList();

        if (candidates.Count == 0)
        {
            return Fail(descriptor, "None of the whitelisted assemblies are loaded.");
        }

        Type? found = null;
        foreach (var assembly in candidates)
        {
            if (descriptor.AssemblyName is { Length: > 0 } specificAsm
                && !string.Equals(assembly.GetName().Name, specificAsm, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = assembly.GetType(descriptor.ComponentTypeName, throwOnError: false, ignoreCase: false);
            if (candidate is null)
            {
                continue;
            }

            if (found is not null && found != candidate)
            {
                return Fail(descriptor, $"Ambiguous component type '{descriptor.ComponentTypeName}' found in multiple whitelisted assemblies.");
            }

            found = candidate;
        }

        if (found is null)
        {
            return Fail(descriptor, $"Component type '{descriptor.ComponentTypeName}' not found in any whitelisted assembly.");
        }

        if (!typeof(IComponent).IsAssignableFrom(found))
        {
            return Fail(descriptor, $"Type '{descriptor.ComponentTypeName}' is not a Blazor component.");
        }

        return new GameTypeExtensionResolution
        {
            Descriptor = descriptor,
            ComponentType = found
        };
    }

    private GameTypeExtensionResolution Fail(GameTypeUiExtensionDescriptor descriptor, string reason)
    {
        _logger.LogWarning("UI extension resolve failed for '{Type}': {Reason}", descriptor.ComponentTypeName, reason);
        return new GameTypeExtensionResolution
        {
            Descriptor = descriptor,
            ComponentType = null,
            FailureReason = reason
        };
    }
}
