using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsPlatform.Application.DTOs;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Api.Controllers;

/// <summary>Endpoint protegido de agentes: /api/v1/auth/login.</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthController(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        // Sin tenant resuelto todavía (no hay JWT ni X-Tenant-Id) -> se busca en todos los tenants activos.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.Tenant.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Credenciales inválidas." });

        var (token, expiresAt) = _jwt.GenerateToken(user);

        return Ok(new LoginResponse(
            token,
            expiresAt,
            user.Id,
            user.TenantId,
            user.Tenant.Slug,
            user.FullName,
            user.Role
        ));
    }
}
