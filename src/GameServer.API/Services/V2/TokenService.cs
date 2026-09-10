using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameServer.API.Configurations;
using GameServer.API.Data.V2;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.API.Services.V2;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(UserEntity user, IEnumerable<string> groupNames);
}

public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwtOptions = options.Value;

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(UserEntity user, IEnumerable<string> groupNames)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var groupName in groupNames)
        {
            claims.Add(new Claim("groups", groupName));
        }

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(tokenDescriptor);

        return (tokenString, expiresAt);
    }
}
