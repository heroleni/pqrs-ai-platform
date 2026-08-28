namespace PqrsPlatform.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }
    bool IsResolved { get; }

    void SetTenant(Guid tenantId, string slug);
}