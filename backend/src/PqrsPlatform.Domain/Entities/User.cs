namespace PqrsPlatform.Domain.Entities;

/// <summary>Agente o administrador. Se autentica con JWT.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt. Nunca la contraseña en claro.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>"Agent" o "Admin". Va como claim en el JWT.</summary>
    public string Role { get; set; } = "Agent";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
