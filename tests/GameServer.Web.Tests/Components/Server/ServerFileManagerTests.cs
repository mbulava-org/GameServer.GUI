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

    [Fact]
    public async Task ServerFileManager_HelperMethodsAndActions_ExecuteCorrectly()
    {
        var volumes = new List<GameServerResolvedVolume>
        {
            new() { ContainerPath = "/data", Source = "vol_data", ReadOnly = false },
            new() { ContainerPath = "/config", Source = "vol_config", ReadOnly = true }
        };

        _filesApiMock.Setup(f => f.ListFilesAsync("srv-1", "/data", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileItem>
            {
                new() { Name = "server.properties", Path = "server.properties", IsDirectory = false, Size = 500, LastModified = DateTime.UtcNow },
                new() { Name = "backup.tar.gz", Path = "backup.tar.gz", IsDirectory = false, Size = 10485760, LastModified = DateTime.UtcNow },
                new() { Name = "logs", Path = "logs", IsDirectory = true, Size = 0, LastModified = DateTime.UtcNow }
            });

        _filesApiMock.Setup(f => f.DeleteAsync("srv-1", "/data", "server.properties", false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cut = Render<ServerFileManager>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.Volumes, volumes));

        var instance = cut.Instance;
        var methodGetIcon = typeof(ServerFileManager).GetMethod("GetFileIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var methodFormat = typeof(ServerFileManager).GetMethod("FormatFileSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var methodEditable = typeof(ServerFileManager).GetMethod("IsEditable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var methodNavigatePath = instance.GetType().GetMethod("NavigateToPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var methodVolumeChanged = instance.GetType().GetMethod("OnVolumeChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var txtFile = new FileItem { Name = "server.txt", Path = "server.txt", IsDirectory = false, Size = 500 };
        var zipFile = new FileItem { Name = "server.zip", Path = "server.zip", IsDirectory = false, Size = 1048576 };
        var imgFile = new FileItem { Name = "icon.png", Path = "icon.png", IsDirectory = false, Size = 2048 };
        var jsonFile = new FileItem { Name = "config.json", Path = "config.json", IsDirectory = false, Size = 100 };
        var dirItem = new FileItem { Name = "sub", Path = "sub", IsDirectory = true, Size = 0 };

        Assert.Equal("description", methodGetIcon!.Invoke(null, [txtFile]));
        Assert.Equal("folder", methodGetIcon.Invoke(null, [dirItem]));
        Assert.Equal("archive", methodGetIcon.Invoke(null, [zipFile]));
        Assert.Equal("image", methodGetIcon.Invoke(null, [imgFile]));
        Assert.Equal("code", methodGetIcon.Invoke(null, [jsonFile]));

        Assert.True((bool)methodEditable!.Invoke(null, [txtFile])!);
        Assert.True((bool)methodEditable.Invoke(null, [jsonFile])!);
        Assert.False((bool)methodEditable.Invoke(null, [zipFile])!);
        Assert.False((bool)methodEditable.Invoke(null, [dirItem])!);

        Assert.Equal("500 B", methodFormat!.Invoke(null, [500L]));
        Assert.Equal("1 MB", methodFormat.Invoke(null, [1048576L]));
        Assert.Equal("1 GB", methodFormat.Invoke(null, [1073741824L]));
        Assert.Equal("1 TB", methodFormat.Invoke(null, [1099511627776L]));
        Assert.Equal("0 B", methodFormat.Invoke(null, [0L]));

        var shFile = new FileItem { Name = "start.sh", Path = "start.sh" };
        var unknownFile = new FileItem { Name = "unknown.bin", Path = "unknown.bin" };
        Assert.Equal("terminal", methodGetIcon.Invoke(null, [shFile]));
        Assert.Equal("insert_drive_file", methodGetIcon.Invoke(null, [unknownFile]));

        // Switch volume
        await (Task)methodVolumeChanged!.Invoke(instance, ["/config"])!;

        // Navigate path
        await (Task)methodNavigatePath!.Invoke(instance, [""])!;
    }

    [Fact]
    public async Task ServerFileManager_WhenLoadErrorOccurs_DisplaysWarningAlert()
    {
        var volumes = new List<GameServerResolvedVolume>
        {
            new() { ContainerPath = "/data", Source = "vol_data" }
        };

        _filesApiMock.Setup(f => f.ListFilesAsync("srv-1", "/data", "", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent offline"));

        var cut = Render<ServerFileManager>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.Volumes, volumes));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Agent offline", cut.Markup);
        });
    }

    [Fact]
    public async Task ServerFileManager_WhenDirectoryIsEmpty_DisplaysEmptyMessage()
    {
        var volumes = new List<GameServerResolvedVolume>
        {
            new() { ContainerPath = "/data", Source = "vol_data" }
        };

        _filesApiMock.Setup(f => f.ListFilesAsync("srv-1", "/data", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FileItem>());

        var cut = Render<ServerFileManager>(p => p
            .Add(x => x.ServerId, "srv-1")
            .Add(x => x.Volumes, volumes));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("This directory is empty.", cut.Markup);
        });
    }
}
