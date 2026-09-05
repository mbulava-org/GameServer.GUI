using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Tests.Services.V2;

public class GameTypeExtensionResolverTests
{
    private static IOptionsMonitor<GameTypeExtensionsOptions> BuildOptions(params string[] allowed)
    {
        var options = new GameTypeExtensionsOptions { AllowedAssemblies = allowed.ToList() };
        return new TestOptionsMonitor(options);
    }

    private static GameTypeExtensionResolver Create(params string[] allowed)
        => new(BuildOptions(allowed), NullLogger<GameTypeExtensionResolver>.Instance);

    [Fact]
    public void Resolve_NullDescriptors_ReturnsEmpty()
    {
        var resolver = Create("GameServer.Web");
        Assert.Empty(resolver.Resolve(null));
    }

    [Fact]
    public void Resolve_ValidBlazorComponent_ReturnsResolved()
    {
        var resolver = Create("GameServer.Web");
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            ComponentTypeName = "GameServer.Web.Components.Server.Extensions.NotYetImplementedTab",
            AssemblyName = "GameServer.Web",
            Title = "Test"
        };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.True(result.IsResolved);
        Assert.NotNull(result.ComponentType);
        Assert.True(typeof(IComponent).IsAssignableFrom(result.ComponentType));
    }

    [Fact]
    public void Resolve_UnknownType_FailsWithReason()
    {
        var resolver = Create("GameServer.Web");
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            ComponentTypeName = "GameServer.Web.Does.Not.Exist",
            AssemblyName = "GameServer.Web",
            Title = "Missing"
        };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.False(result.IsResolved);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void Resolve_NonComponentType_FailsWithReason()
    {
        var resolver = Create("GameServer.Web");
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            // Any non-IComponent type in GameServer.Web works.
            ComponentTypeName = typeof(GameTypeUiExtensionDescriptor).FullName!,
            AssemblyName = "GameServer.Web",
            Title = "NotAComponent"
        };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.False(result.IsResolved);
        Assert.Contains("not a Blazor component", result.FailureReason);
    }

    [Fact]
    public void Resolve_DisallowedAssembly_FailsWithReason()
    {
        var resolver = Create("GameServer.Web");
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            ComponentTypeName = "GameServer.Web.Components.Server.Extensions.NotYetImplementedTab",
            AssemblyName = "SomeOther.Assembly",
            Title = "Blocked"
        };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.False(result.IsResolved);
        Assert.Contains("whitelist", result.FailureReason);
    }

    [Fact]
    public void Resolve_EmptyWhitelist_Fails()
    {
        var resolver = Create();
        var descriptor = new GameTypeUiExtensionDescriptor
        {
            ComponentTypeName = "GameServer.Web.Components.Server.Extensions.NotYetImplementedTab",
            Title = "NoWhitelist"
        };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.False(result.IsResolved);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void Resolve_MissingComponentTypeName_Fails()
    {
        var resolver = Create("GameServer.Web");
        var descriptor = new GameTypeUiExtensionDescriptor { Title = "Empty" };

        var result = Assert.Single(resolver.Resolve(new[] { descriptor }));

        Assert.False(result.IsResolved);
        Assert.Contains("ComponentTypeName", result.FailureReason);
    }

    [Fact]
    public void Resolve_OrdersByOrderThenTitle()
    {
        var resolver = Create("GameServer.Web");
        var descriptors = new[]
        {
            new GameTypeUiExtensionDescriptor { Title = "B", Order = 20, ComponentTypeName = "x" },
            new GameTypeUiExtensionDescriptor { Title = "A", Order = 10, ComponentTypeName = "x" },
            new GameTypeUiExtensionDescriptor { Title = "A2", Order = 10, ComponentTypeName = "x" }
        };

        var results = resolver.Resolve(descriptors);

        Assert.Equal(new[] { "A", "A2", "B" }, results.Select(r => r.Descriptor.Title));
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<GameTypeExtensionsOptions>
    {
        public TestOptionsMonitor(GameTypeExtensionsOptions value) => CurrentValue = value;
        public GameTypeExtensionsOptions CurrentValue { get; }
        public GameTypeExtensionsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<GameTypeExtensionsOptions, string?> listener) => null;
    }
}
