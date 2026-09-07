namespace GameServer.API.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "GameServer_Super_Secret_Key_At_Least_32_Bytes_Long_2026!";
    public string Issuer { get; set; } = "GameServer.API";
    public string Audience { get; set; } = "GameServer.Web";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours
}
