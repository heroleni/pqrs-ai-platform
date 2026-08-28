using PqrsPlatform.Domain.Interfaces;

namespace PqrsPlatform.Infrastructure.Tenancy;
public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsResolved => TenantId != Guid.Empty;

    public void SetTenant(Guid tenantId, string slug)
    {
        TenantId = tenantId;
        Slug = slug;
    }
}