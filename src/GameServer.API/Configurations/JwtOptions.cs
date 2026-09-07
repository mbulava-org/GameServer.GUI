namespace GameServer.API.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "GameServer.API";
    public string Audience { get; set; } = "GameServer.Web";
    public int ExpiryMinutes { get; set; } = 1440; // 24 hours
}
