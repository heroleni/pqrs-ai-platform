using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;

namespace PqrsPlatform.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(User user)
    {
        var secret = _config["JWT_SECRET"] ?? _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Falta JWT_SECRET/Jwt:Secret en la configuración.");
        var issuer = _config["JWT_ISSUER"] ?? _config["Jwt:Issuer"] ?? "pqrs-platform";
        var audience = _config["JWT_AUDIENCE"] ?? _config["Jwt:Audience"] ?? "pqrs-platform";
        var minutes = int.TryParse(_config["JWT_EXPIRATION_MINUTES"] ?? _config["Jwt:ExpirationMinutes"], out var m) ? m : 480;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("full_name", user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
