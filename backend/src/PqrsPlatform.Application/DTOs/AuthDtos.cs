namespace PqrsPlatform.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    Guid TenantId,
    string TenantSlug,
    string FullName,
    string Role
);
