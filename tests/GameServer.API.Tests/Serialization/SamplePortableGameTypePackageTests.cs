using System.Text.Json;
using GameServer.API.Dtos.V2;

namespace GameServer.API.Tests.Serialization;

public class SamplePortableGameTypePackageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("palworld-dedicated.portable.json", "palworld-dedicated", "thijsvanloef/palworld-server-docker")]
    [InlineData("minecraft-bedrock.portable.json", "minecraft-bedrock", "itzg/minecraft-bedrock-server")]
    [InlineData("minecraft-java.portable.json", "minecraft-java", "itzg/minecraft-server")]
    [InlineData("conan-exiles-dedicated.portable.json", "conan-exiles-dedicated", "othrayte/docker-conanexiles")]
    public void SamplePackage_DeserializesCorrectly(string fileName, string expectedKey, string expectedImage)
    {
        // Arrange
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Search upward for docs/samples/gametype-imports
        var searchDir = new DirectoryInfo(baseDir);
        string? filePath = null;

        while (searchDir != null)
        {
            var candidate = Path.Combine(searchDir.FullName, "docs", "samples", "gametype-imports", fileName);
            if (File.Exists(candidate))
            {
                filePath = candidate;
                break;
            }
            searchDir = searchDir.Parent;
        }

        Assert.True(filePath != null && File.Exists(filePath), $"Could not locate sample file: {fileName}");

        // Act
        var json = File.ReadAllText(filePath);
        var package = JsonSerializer.Deserialize<PortableGameTypePackageDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(package);
        Assert.Equal("1.0", package.FormatVersion);
        Assert.Equal(expectedKey, package.GameType.Key);
        Assert.NotEmpty(package.GameType.DisplayName);
        Assert.NotEmpty(package.GameType.Revisions);

        var revision = package.GameType.Revisions[0];
        Assert.Equal(expectedImage, revision.ImageReference);
        Assert.NotEmpty(revision.Ports);
        Assert.NotEmpty(revision.Volumes);
        Assert.NotEmpty(revision.SettingDefinitions);
    }
}
