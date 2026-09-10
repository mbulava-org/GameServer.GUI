using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GameServer.API.Configurations;
using GameServer.API.Data.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public sealed class TokenServiceAndHasherTests
{
    [Fact]
    public void PasswordHasher_HashesAndVerifiesPasswordCorrectly()
    {
        // Arrange
        var hasher = new PasswordHasher();
        const string password = "SecurePassword123!";

        // Act
        var hash = hasher.HashPassword(password);
        var isValid = hasher.VerifyPassword(hash, password);
        var isInvalid = hasher.VerifyPassword(hash, "WrongPassword");

        // Assert
        Assert.NotEmpty(hash);
        Assert.Contains(".", hash);
        Assert.True(isValid);
        Assert.False(isInvalid);

        const string seedHash = "100000.AQIDBAUGBwgJCgsMDQ4PEA==.HcXuAK10SjWLJPp2fjPLGDZzYBUHjuLTj/VUPgmfEuE=";
        Assert.True(hasher.VerifyPassword(seedHash, "Admin123!"), "Admin123! should verify against the seeded admin password hash");
    }

    [Fact]
    public void TokenService_CreatesValidJwtToken_WithClaims()
    {
        // Arrange
        var jwtOptions = Options.Create(new JwtOptions
        {
            SecretKey = "SuperSecretKeyForTestingJwtAuthentication1234567890!",
            Issuer = "GameServer.API.Tests",
            Audience = "GameServer.Web.Tests",
            ExpiryMinutes = 60
        });

        var tokenService = new TokenService(jwtOptions);

        var user = new UserEntity
        {
            Id = 42,
            Username = "testuser",
            Email = "test@example.com",
            Role = "GameManager"
        };

        // Act
        var (token, expiresAt) = tokenService.GenerateToken(user, ["Default", "Managers"]);

        // Assert
        Assert.NotEmpty(token);
        Assert.True(expiresAt > DateTime.UtcNow);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal("GameServer.API.Tests", jwtToken.Issuer);
        Assert.Contains(jwtToken.Audiences, a => a == "GameServer.Web.Tests");

        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(subClaim);
        Assert.Equal("42", subClaim.Value);

        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal("testuser", nameClaim.Value);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal("GameManager", roleClaim.Value);

        var groupClaims = jwtToken.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList();
        Assert.Contains("Default", groupClaims);
        Assert.Contains("Managers", groupClaims);
    }
}
