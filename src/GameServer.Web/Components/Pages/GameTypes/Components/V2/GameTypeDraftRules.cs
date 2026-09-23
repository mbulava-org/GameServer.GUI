namespace GameServer.Web.Components.Pages.GameTypes.Components.V2;

public static class GameTypeDraftRules
{
    public const int KeyMaxLength = 100;
    public const int DisplayNameMaxLength = 200;
    public const int TypeMaxLength = 50;
    public const int ThumbnailUrlMaxLength = 500;
    public const int DocumentationUrlMaxLength = 500;

    public const int VersionTagMaxLength = 100;
    public const int ImageReferenceMaxLength = 500;
    public const int ReadyLogPatternMaxLength = 500;
    public const int ImageDigestMaxLength = 250;

    public const int PortProtocolMaxLength = 10;

    public const int VolumeSourceMaxLength = 500;
    public const int VolumeUsageMaxLength = 100;
    public const int VolumeMountTypeMaxLength = 50;
    public const int VolumeVariableMaxLength = 200;
    public const int VolumePermissionsMaxLength = 10;

    public const int SettingKeyMaxLength = 200;
    public const int SettingDataTypeMaxLength = 50;
    public const int SettingCategoryMaxLength = 100;

    public const int WebHostNameMaxLength = 200;
    public const int WebHostPathSegmentMaxLength = 200;
    public const int WebHostPortVariableMaxLength = 200;
    public const int WebHostEnabledWhenMaxLength = 500;

    public static List<string> ValidateBasicInfo(
        string? key,
        string? displayName,
        string? type,
        string? thumbnailUrl,
        string? documentationUrl)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(key))
        {
            issues.Add("Key is required.");
        }
        else if (key.Length > KeyMaxLength)
        {
            issues.Add($"Key cannot exceed {KeyMaxLength} characters (currently {key.Length}).");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            issues.Add("Display Name is required.");
        }
        else if (displayName.Length > DisplayNameMaxLength)
        {
            issues.Add($"Display Name cannot exceed {DisplayNameMaxLength} characters (currently {displayName.Length}).");
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            issues.Add("Type is required.");
        }
        else if (type.Length > TypeMaxLength)
        {
            issues.Add($"Type cannot exceed {TypeMaxLength} characters (currently {type.Length}).");
        }

        if (thumbnailUrl is not null && thumbnailUrl.Length > ThumbnailUrlMaxLength)
        {
            issues.Add($"Thumbnail URL cannot exceed {ThumbnailUrlMaxLength} characters (currently {thumbnailUrl.Length}).");
        }

        if (documentationUrl is not null && documentationUrl.Length > DocumentationUrlMaxLength)
        {
            issues.Add($"Documentation URL cannot exceed {DocumentationUrlMaxLength} characters (currently {documentationUrl.Length}).");
        }

        return issues;
    }
}
