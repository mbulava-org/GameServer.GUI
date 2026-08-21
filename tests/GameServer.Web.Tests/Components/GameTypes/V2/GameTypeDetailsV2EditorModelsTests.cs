using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public class GameTypeDetailsV2EditorModelsTests
{
    [Fact]
    public void BuildPathSegmentFromName_ShouldGenerateSlug()
    {
        Assert.Equal("web-map-ui", GameTypeRevisionWebHostDraftRules.BuildPathSegmentFromName("Web Map UI"));
        Assert.Equal("admin-panel", GameTypeRevisionWebHostDraftRules.BuildPathSegmentFromName("---Admin---Panel---"));
        Assert.Equal(string.Empty, GameTypeRevisionWebHostDraftRules.BuildPathSegmentFromName(null));
        Assert.Equal(string.Empty, GameTypeRevisionWebHostDraftRules.BuildPathSegmentFromName("   "));
    }

    [Fact]
    public void GetPathSegmentValidationIssues_WhenValid_ShouldReturnEmpty()
    {
        var issues = GameTypeRevisionWebHostDraftRules.GetPathSegmentValidationIssues("map/{serverId}/live");
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(" map", "cannot start or end with whitespace")]
    [InlineData("/map", "cannot start or end with '/'")]
    [InlineData("map/", "cannot start or end with '/'")]
    [InlineData("map//live", "cannot contain empty path segments")]
    [InlineData("map/{unsupportedVar}", "unsupported runtime variable")]
    [InlineData("map/{bad_var", "malformed runtime variable")]
    [InlineData("map/Invalid_Caps", "can only contain lowercase letters")]
    public void GetPathSegmentValidationIssues_WhenInvalid_ShouldReturnExpectedIssue(string segment, string expectedSnippet)
    {
        var issues = GameTypeRevisionWebHostDraftRules.GetPathSegmentValidationIssues(segment);
        Assert.Contains(issues, issue => issue.Contains(expectedSnippet, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameTypeRevisionListRow_Properties_ShouldSetAndGet()
    {
        var row = new GameTypeRevisionListRow
        {
            Id = 1,
            VersionTag = "1.0.0",
            ImageDigest = "sha256:1234",
            IsPublished = true,
            EnableTTY = true,
            CreatedAt = DateTime.UtcNow,
            IsUnsavedDraft = false,
            SourceRevision = new()
        };

        Assert.Equal(1, row.Id);
        Assert.Equal("1.0.0", row.VersionTag);
        Assert.Equal("sha256:1234", row.ImageDigest);
        Assert.True(row.IsPublished);
        Assert.True(row.EnableTTY);
        Assert.False(row.IsUnsavedDraft);
        Assert.NotNull(row.SourceRevision);
    }

    [Fact]
    public void GameTypeRevisionVolumeDraft_Properties_ShouldSetAndGet()
    {
        var draft = new GameTypeRevisionVolumeDraft
        {
            Source = "/data",
            Description = "Main Data",
            Usage = "data",
            MountType = "nfs",
            ReadOnly = false,
            OwnerUid = 1000,
            OwnerGid = 1000,
            OwnerUidVariable = "UID",
            OwnerGidVariable = "GID",
            Permissions = "0755",
            EnsureNfsPathExists = true,
            Required = true
        };

        Assert.Equal("/data", draft.Source);
        Assert.Equal("nfs", draft.MountType);
        Assert.Equal(1000, draft.OwnerUid);
        Assert.Equal(1000, draft.OwnerGid);
        Assert.Equal("UID", draft.OwnerUidVariable);
        Assert.Equal("GID", draft.OwnerGidVariable);
        Assert.Equal("0755", draft.Permissions);
        Assert.True(draft.EnsureNfsPathExists);
        Assert.True(draft.Required);
    }

    [Fact]
    public void GameTypeRevisionSettingDraft_Properties_ShouldSetAndGet()
    {
        var draft = new GameTypeRevisionSettingDraft
        {
            SettingKey = "MAX_PLAYERS",
            DefaultValue = "20",
            Description = "Max player count",
            Metadata = new GameTypeRevisionSettingMetadataDraft
            {
                DataType = "number",
                Category = "Gameplay",
                IsRequired = true,
                CannotBeEmpty = true,
                Placeholder = "20",
                ValidationPattern = "^[0-9]+$",
                ValidationMessage = "Must be a number",
                AutoAllocatePort = false,
                ValidateRelatedPortsAvailability = false,
                AllowedValuesJson = "[]",
                ValueMappingsJson = "{}",
                EnumUnderlyingType = "string",
                EnumValues = [new EnumValueDraft { Value = "1", DisplayLabel = "One" }],
                PortMappings = [new GameTypeRevisionPortMappingDraft { TargetContainerPort = 8080, TargetProtocol = "tcp", IsRequired = true }]
            }
        };

        Assert.Equal("MAX_PLAYERS", draft.SettingKey);
        Assert.Equal("20", draft.DefaultValue);
        Assert.Equal("number", draft.Metadata.DataType);
        Assert.Single(draft.Metadata.EnumValues);
        Assert.Single(draft.Metadata.PortMappings);
    }
}
