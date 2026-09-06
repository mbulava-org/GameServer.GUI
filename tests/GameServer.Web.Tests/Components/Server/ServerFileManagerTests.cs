using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;
using Radzen.Blazor;

namespace GameServer.Web.Tests.Components.Server;

public sealed class ServerFileManagerTests : BunitContext
{
    private readonly Mock<IGameServerFilesApiService> _filesApiMock = new();

    public ServerFileManagerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<DialogService>();
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton(_filesApiMock.Object);
    }

    [Fact]
    public void ServerFileManager_WhenNoVolumes_ShowsNoVolumesAlert()
    {
        var cut = Render<ServerFileManager>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.Volumes, []));

        Assert.Contains("No storage volumes are configured for this server", cut.Markup);
    }

    [Fact]
    public async Task ServerFileManager_WhenVolumesProvided_RendersToolbarAndNavigates()
    {
        var volumes = new List<GameServerResolvedVolume>
        {
            new() { ContainerPath = "/data", Source = "vol_data", ReadOnly = false }
        };

        _filesApiMock.Setup(f => f.ListFilesAsync("srv-1", "/data", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileItem>
            {
                new() { Name = "config.json", Path = "config.json", IsDirectory = false, Size = 1024, LastModified = DateTime.UtcNow },
                new() { Name = "saves", Path = "saves", IsDirectory = true, Size = 0, LastModified = DateTime.UtcNow }
            });

        _filesApiMock.Setup(f => f.ListFilesAsync("srv-1", "/data", "saves", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileItem>
            {
                new() { Name = "world.db", Path = "saves/world.db", IsDirectory = false, Size = 2048, LastModified = DateTime.UtcNow }
            });

        _filesApiMock.Setup(f => f.DownloadAsync("srv-1", "/data", "config.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        var cut = Render<ServerFileManager>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.Volumes, volumes));

        Assert.Contains("Storage Volume", cut.Markup);
        Assert.Contains("New Folder", cut.Markup);
        Assert.Contains("Upload", cut.Markup);
        Assert.Contains("root", cut.Markup);
        Assert.Contains("config.json", cut.Markup);
        Assert.Contains("saves", cut.Markup);

        // Click on directory link
        var dirLink = cut.FindAll("a").FirstOrDefault(a => a.TextContent.Trim() == "saves");
        Assert.NotNull(dirLink);
        await cut.InvokeAsync(() => dirLink.Click());

        // Click on download button
        var downloadBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Icon == "download");
        if (downloadBtn != null)
        {
            await cut.InvokeAsync(() => downloadBtn.Find("button").Click());
        }
    }

    [Fact]
    public async Task FileEditorDialog_WhenLoaded_RendersContentAndSaves()
    {
        _filesApiMock.Setup(f => f.GetContentAsync("srv-1", "/data", "server.properties", It.IsAny<CancellationToken>()))
            .ReturnsAsync("server-port=25565\nmotd=A Minecraft Server");

        _filesApiMock.Setup(f => f.SaveContentAsync("srv-1", "/data", "server.properties", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cut = Render<FileEditorDialog>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.VolumePath, "/data")
            .Add(x => x.FilePath, "server.properties")
            .Add(x => x.FileName, "server.properties"));

        Assert.Contains("server.properties", cut.Markup);
        Assert.Contains("Cancel", cut.Markup);
        Assert.Contains("Save", cut.Markup);

        var saveBtn = cut.FindComponents<RadzenButton>().FirstOrDefault(b => b.Instance.Text == "Save");
        Assert.NotNull(saveBtn);
        await cut.InvokeAsync(() => saveBtn.Find("button").Click());

        _filesApiMock.Verify(f => f.SaveContentAsync("srv-1", "/data", "server.properties", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
