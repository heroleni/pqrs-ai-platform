using  Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Api.Cors;

public class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    private const string CacheKey = "cors:allowed-origins";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public DynamicCorsPolicyProvider(IMemoryCache cache) => _cache = cache;

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origin = context.Request.Headers.Origin.ToString();
        
        if (string.IsNullOrWhiteSpace(origin))
            return null;

        var allowed = await GetAllowedOriginsAsync(context);

        var builder = new CorsPolicyBuilder();

        if (allowed.Contains(Normalize(origin)))
        {
            builder.WithOrigins(origin)
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();   // SignalR lo exige para negociar
        }


        return builder.Build();
    }

    private async Task<HashSet<string>> GetAllowedOriginsAsync(HttpContext context)
    {
        if (_cache.TryGetValue(CacheKey, out HashSet<string>? cached) && cached is not null)
            return cached;

        var db = context.RequestServices.GetRequiredService<AppDbContext>();

        var listas = await db.Tenants
            .IgnoreQueryFilters()         
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.AllowedOrigins)
            .ToListAsync();
        
        var origins = listas.SelectMany(x => x).ToList();

        var set = origins
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _cache.Set(CacheKey, set, CacheFor);
        return set;
    }
    
    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}