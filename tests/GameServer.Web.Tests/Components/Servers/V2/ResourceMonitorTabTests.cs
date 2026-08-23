using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Radzen;
using Xunit;

namespace GameServer.Web.Tests.Components.Servers.V2;

public class ResourceMonitorTabTests : BunitContext
{
    public ResourceMonitorTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<ILogger<ResourceMonitorTab>>(NullLogger<ResourceMonitorTab>.Instance);
        Services.AddSingleton(Options.Create(new GameServerDockerApi { BaseUri = "http://localhost:5164" }));
    }

    [Fact]
    public void ResourceMonitorTab_RendersHeaderAndTitle()
    {
        var cut = Render<ResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1"));

        Assert.Contains("Live Resource Usage", cut.Markup);
    }
}
