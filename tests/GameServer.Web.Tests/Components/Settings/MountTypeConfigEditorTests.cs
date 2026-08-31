using Bunit;
using GameServer.Web.Components.Pages.Settings;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radzen;

namespace GameServer.Web.Tests.Components.Settings;

public sealed class MountTypeConfigEditorTests : BunitContext
{
    private readonly Mock<IMountTypeConfigApiService> api = new();

    public MountTypeConfigEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton(api.Object);
    }

    [Fact]
    public void MountTypeConfigEditor_WhenConfigsLoaded_ShouldRenderListAndDetails()
    {
        // Arrange
        var configs = new List<MountTypeConfig>
        {
            new()
            {
                Key = "nfs",
                DisplayName = "NFS Mount",
                Description = "NFS Export mount",
                VolumeNameFormat = "{gameTypeKey}-{serverId}-{Source}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Options = new Dictionary<string, string>
                {
                    ["Driver"] = "local"
                }
            }
        };

        api.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configs);

        // Act
        var cut = Render<MountTypeConfigEditor>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mount Type Configuration", cut.Markup);
            Assert.Contains("NFS Mount", cut.Markup);
            Assert.Contains("Volume Name Format", cut.Markup);
            Assert.Contains("Live Preview", cut.Markup);
        });
    }

    [Fact]
    public void MountTypeConfigEditor_AddNewConfig_ShouldAddDraft()
    {
        // Arrange
        api.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<MountTypeConfig>());
        var cut = Render<MountTypeConfigEditor>();

        // Act
        cut.WaitForAssertion(() => Assert.Contains("New Mount Type", cut.Markup));
        var newButton = cut.FindAll("button").First(b => b.TextContent.Contains("New Mount Type"));
        newButton.Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New mount type", cut.Markup);
            Assert.Contains("Resolved Volume Name", cut.Markup);
        });
    }

    [Fact]
    public void MountTypeConfigEditor_SaveAsync_ShouldInvokeSaveApi()
    {
        // Arrange
        var config = new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Named Volume",
            VolumeNameFormat = "{gameTypeKey}_{serverId}_{Source}",
            IsActive = true,
            Options = new Dictionary<string, string>()
        };

        api.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([config]);
        api.Setup(a => a.SaveAsync(It.IsAny<MountTypeConfig>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var cut = Render<MountTypeConfigEditor>();

        // Act
        cut.WaitForAssertion(() => Assert.Contains("Save Changes", cut.Markup));
        var saveButton = cut.FindAll("button").First(b => b.TextContent.Contains("Save Changes"));
        saveButton.Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            api.Verify(a => a.SaveAsync(It.IsAny<MountTypeConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Fact]
    public void MountTypeConfigEditor_DeleteAsync_ShouldInvokeDeleteApi()
    {
        // Arrange
        var config = new MountTypeConfig
        {
            Key = "custom",
            DisplayName = "Custom Mount",
            VolumeNameFormat = "{gameTypeKey}_{serverId}_{Source}",
            IsActive = true
        };

        api.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([config]);
        api.Setup(a => a.DeleteAsync("custom", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cut = Render<MountTypeConfigEditor>();

        // Act
        cut.WaitForAssertion(() => Assert.Contains("Custom Mount", cut.Markup));
        var deleteButton = cut.FindAll("button").First(b => b.ClassList.Contains("rz-danger"));
        deleteButton.Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            api.Verify(a => a.DeleteAsync("custom", It.IsAny<CancellationToken>()), Times.Once);
        });
    }
}
