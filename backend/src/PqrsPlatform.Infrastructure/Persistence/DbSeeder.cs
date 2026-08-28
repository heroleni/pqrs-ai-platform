using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;

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

        var demo = await EnsureTenantAsync(db, logger, DemoSlug, "Velocidad Motors", LocalOrigins, ct);

        // Segundo tenant: existe para poder demostrar el aislamiento.
        // Sus artículos y tickets no deben aparecer nunca al consultar como demo.
        var second = await EnsureTenantAsync(db, logger, SecondSlug, "MóvilNet Telecomunicaciones",
            new[] { "http://localhost:5501" }, ct);

        await EnsureAdminAsync(db, logger, demo, DemoAdminEmail, "Admin Demo", ct);
        await EnsureAdminAsync(db, logger, second, SecondAdminEmail, "Admin MóvilNet", ct);

        await EnsureArticlesAsync(db, embeddings, logger, demo, new[]
        {
            ("Garantía del vehículo",
             "La garantía cubre 5 años o 100.000 kilómetros, lo que ocurra primero, en vehículos " +
             "eléctricos e híbridos, y 3 años en vehículos a gasolina. Cubre defectos de fábrica en " +
             "motor, transmisión y sistema eléctrico. No cubre desgaste normal de llantas, pastillas " +
             "de freno ni daños por mal uso."),

            ("Mantenimiento programado",
             "El primer mantenimiento se realiza a los 5.000 kilómetros y es gratuito. Después se " +
             "programa cada 10.000 kilómetros o cada seis meses. Puedes agendar por la línea " +
             "018000-123456 o desde este asistente."),

            ("Financiación y crédito",
             "Ofrecemos planes de financiación hasta 72 meses con cuota inicial desde el 20% del valor " +
             "del vehículo. La aprobación se hace en línea en menos de 24 horas hábiles y requiere " +
             "cédula, certificación laboral y extractos bancarios de los últimos tres meses."),

            ("Prueba de manejo",
             "Puedes agendar una prueba de manejo sin costo presentando licencia de conducción vigente. " +
             "La prueba dura 30 minutos e incluye recorrido urbano. Se agenda con 24 horas de " +
             "anticipación en la sede de Medellín."),

            ("Retoma de vehículo usado",
             "Recibimos tu vehículo usado como parte de pago. El avalúo es gratuito, toma alrededor de " +
             "40 minutos e incluye revisión mecánica y de documentos. El valor aprobado se abona " +
             "directamente a la cuota inicial."),

            ("Horario de atención",
             "Atendemos de lunes a viernes de 8:00 a.m. a 6:00 p.m. y sábados de 8:00 a.m. a 1:00 p.m. " +
             "El taller cierra los domingos y festivos.")
        }, ct);

        await EnsureArticlesAsync(db, embeddings, logger, second, new[]
        {
            ("Portabilidad numérica",
             "La portabilidad tarda entre 1 y 3 días hábiles. Debes estar a paz y salvo con tu " +
             "operador actual.")
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
                // después desde el panel o volviendo a guardarlo.
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
                Subject = "Falla en el vehículo recién entregado",
                Description = "El carro que recibí hace dos semanas presenta una falla en la transmisión. " +
                              "Es la segunda vez que lo llevo al taller y nadie me responde. " +
                              "Exijo una solución inmediata.",
                Type = Domain.Enums.TicketType.Reclamo,
                Priority = Domain.Enums.TicketPriority.Alta,
                Sentiment = Domain.Enums.Sentiment.Negativo,
                Status = Domain.Enums.TicketStatus.Pendiente,
                Summary = "Falla de transmisión reincidente en vehículo nuevo, sin respuesta del taller."
            },
            new Ticket
            {
                TenantId = second.Id,
                TicketNumber = "PQRS-2026-0001",
                CustomerName = "Marta Lopera",
                CustomerEmail = "marta@example.com",
                Subject = "Consulta de planes de datos",
                Description = "Quisiera conocer los planes de datos disponibles para este mes.",
                Type = Domain.Enums.TicketType.Peticion,
                Priority = Domain.Enums.TicketPriority.Baja,
                Sentiment = Domain.Enums.Sentiment.Neutro,
                Status = Domain.Enums.TicketStatus.Pendiente,
                Summary = "Solicita información sobre planes de datos vigentes."
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Tickets semilla creados para ambos tenants.");
    }
}