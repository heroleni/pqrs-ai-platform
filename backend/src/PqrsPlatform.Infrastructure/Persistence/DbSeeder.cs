using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Enums;

namespace PqrsPlatform.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string DemoSlug = "demo";
    public const string DemoAdminEmail = "admin@demo.local";
    public const string DemoAdminPassword = "Admin123!";

    public const string SecondSlug = "movilnet";
    public const string SecondAdminEmail = "admin@movilnet.local";

    private static readonly string[] LocalOrigins =
    {
        "http://localhost:5500",
        "http://127.0.0.1:5500",
        "http://localhost:8081",
        "http://127.0.0.1:8081"
    };

    public static async Task SeedAsync(
        AppDbContext db, IEmbeddingService embeddings, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        var demo = await EnsureTenantAsync(db, logger, DemoSlug, "Empresa Demo", LocalOrigins, ct);

        // Segundo tenant: sirve para demostrar el aislamiento. Sus datos NO
        // deben aparecer al consultar con el token del primero.
        var second = await EnsureTenantAsync(db, logger, SecondSlug, "MóvilNet Telecomunicaciones",
            new[] { "http://localhost:5501" }, ct);

        await EnsureAdminAsync(db, logger, demo, DemoAdminEmail, "Admin Demo", ct);
        await EnsureAdminAsync(db, logger, second, SecondAdminEmail, "Admin MóvilNet", ct);

        await EnsureArticlesAsync(db, embeddings, logger, demo, new[]
        {
            ("Horario de atención",
             "Nuestro horario de atención es de lunes a viernes de 8:00 a.m. a 6:00 p.m. y sábados de 8:00 a.m. a 12:00 m."),
            ("Política de devoluciones",
             "Puedes solicitar la devolución de un producto dentro de los 30 días siguientes a la compra, presentando la factura."),
            ("Tiempos de respuesta de PQRS",
             "Las peticiones y quejas se responden en un máximo de 15 días hábiles, según la normativa vigente."),
            ("Cambio de fecha de pago",
             "Puedes cambiar tu fecha de pago desde el portal, en Mi cuenta, Facturación, Fecha de corte. El cambio aplica al siguiente ciclo y solo puede hacerse una vez cada seis meses."),
            ("Cómo reportar una falla del servicio",
             "Reporta la falla por la línea 018000-123456 o desde este widget. Si compromete la vía pública se marca como prioridad alta y se atiende en menos de 4 horas.")
        }, ct);

        await EnsureArticlesAsync(db, embeddings, logger, second, new[]
        {
            ("Portabilidad numérica",
             "La portabilidad tarda entre 1 y 3 días hábiles. Debes estar a paz y salvo con tu operador actual.")
        }, ct);

        await EnsureTicketsAsync(db, logger, demo, second, ct);
    }

    private static async Task<Tenant> EnsureTenantAsync(
        AppDbContext db, ILogger logger, string slug, string name, string[] origins, CancellationToken ct)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Slug == slug, ct);

        if (tenant is not null) return tenant;

        tenant = new Tenant
        {
            Name = name,
            Slug = slug,
            AllowedOrigins = origins.ToList(),
            IsActive = true
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tenant creado: {Slug}", slug);

        return tenant;
    }

    private static async Task EnsureAdminAsync(
        AppDbContext db, ILogger logger, Tenant tenant, string email, string fullName, CancellationToken ct)
    {
        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenant.Id && u.Email == email, ct);

        if (exists) return;

        db.Users.Add(new User
        {
            TenantId = tenant.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoAdminPassword),
            FullName = fullName,
            Role = "Admin"
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Usuario admin creado: {Email}", email);
    }

    private static async Task EnsureArticlesAsync(
        AppDbContext db, IEmbeddingService embeddings, ILogger logger,
        Tenant tenant, (string Title, string Content)[] articles, CancellationToken ct)
    {
        var exists = await db.KnowledgeBaseArticles.IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenant.Id, ct);

        if (exists) return;

        foreach (var (title, content) in articles)
        {
            Pgvector.Vector? embedding = null;

            try
            {
                embedding = await embeddings.EmbedAsync($"{title}: {content}", ct);
            }
            catch (Exception ex)
            {
                // El artículo se guarda igual, sin embedding. Se puede generar
                // después con POST /api/v1/kb-articles/reindex.
                logger.LogWarning(ex,
                    "No se pudo generar embedding para '{Title}'. El artículo queda sin indexar.", title);
            }

            db.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle
            {
                TenantId = tenant.Id,
                Title = title,
                Content = content,
                Embedding = embedding
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Artículos semilla creados para {Slug}.", tenant.Slug);
    }

    private static async Task EnsureTicketsAsync(
        AppDbContext db, ILogger logger, Tenant demo, Tenant second, CancellationToken ct)
    {
        var exists = await db.Tickets.IgnoreQueryFilters().AnyAsync(ct);

        if (exists) return;

        // Los dos comparten número a propósito: el número es único POR tenant.
        // Es la evidencia visible de que las tablas están particionadas por TenantId.
        db.Tickets.AddRange(
            new Ticket
            {
                TenantId = demo.Id,
                TicketNumber = "PQRS-2026-0001",
                CustomerName = "Pedro Ramírez",
                CustomerEmail = "pedro@example.com",
                Subject = "Falla del servicio en la calle 45",
                Description = "El servicio lleva dos días suspendido en todo el sector y nadie responde.",
                Type = TicketType.Reclamo,
                Priority = TicketPriority.Alta,
                Sentiment = Sentiment.Negativo,
                Status = TicketStatus.Pendiente,
                Summary = "Servicio suspendido dos días en el sector, sin respuesta previa."
            },
            new Ticket
            {
                TenantId = second.Id,
                TicketNumber = "PQRS-2026-0001",
                CustomerName = "Marta Lopera",
                CustomerEmail = "marta@example.com",
                Subject = "Consulta de planes de datos",
                Description = "Quisiera conocer los planes de datos disponibles para este mes.",
                Type = TicketType.Peticion,
                Priority = TicketPriority.Baja,
                Sentiment = Sentiment.Neutro,
                Status = TicketStatus.Pendiente,
                Summary = "Solicita información sobre planes de datos vigentes."
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tickets semilla creados para ambos tenants.");
    }
}
