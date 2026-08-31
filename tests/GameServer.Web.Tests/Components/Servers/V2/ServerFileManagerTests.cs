using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Xunit;

namespace GameServer.Web.Tests.Components.Servers.V2;

public class ServerFileManagerTests : BunitContext
{
    private readonly Mock<IGameServerFilesApiService> _filesApiMock;

    public ServerFileManagerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _filesApiMock = new Mock<IGameServerFilesApiService>();

        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<IGameServerFilesApiService>(_filesApiMock.Object);
    }

    [Fact]
    public void ServerFileManager_WhenNoVolumes_ShowsNoVolumesAlert()
    {
        var cut = Render<ServerFileManager>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.Volumes, []));

        Assert.Contains("No storage volumes are configured", cut.Markup);
    }

    [Fact]
    public void ServerFileManager_WhenVolumesProvided_RendersVolumeSelectorAndFileList()
    {
        var volumes = new List<GameServerResolvedVolume>
        {
            new() { ContainerPath = "/data", Usage = "Game Data", VolumeName = "vol-data" }
        };

        _filesApiMock.Setup(api => api.ListFilesAsync("srv-1", "/data", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FileItem { Name = "server.properties", Path = "/server.properties", Size = 512, IsDirectory = false, LastModified = DateTime.UtcNow }
            ]);

        var cut = Render<ServerFileManager>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.Volumes, volumes));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Storage Volume", cut.Markup);
            Assert.Contains("server.properties", cut.Markup);
        });
    }
}
