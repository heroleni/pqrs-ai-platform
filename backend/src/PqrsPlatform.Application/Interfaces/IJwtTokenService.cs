using PqrsPlatform.Domain.Entities;

namespace PqrsPlatform.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Genera un JWT con claims de usuario, tenant y rol. Devuelve el token y su expiración UTC.</summary>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
