using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PqrsPlatform.Domain.Interfaces;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Api.Middleware;

public class TenantResolutionMiddleware
{
    private const string HeaderName = "X-Tenant-Id";
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantContext, AppDbContext db)
    {
        var claim = ctx.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var fromClaim))
        {
            tenantContext.SetTenant(fromClaim, string.Empty);
            await _next(ctx);
            return;
        }
        
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var slugHeader))
        {
            var slug = slugHeader.ToString().Trim();
            
            var tenant = await db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

            if (tenant is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "Tenant no válido o inactivo." });
                return;
            }

            tenantContext.SetTenant(tenant.Id, tenant.Slug);
        }

        await _next(ctx);
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}