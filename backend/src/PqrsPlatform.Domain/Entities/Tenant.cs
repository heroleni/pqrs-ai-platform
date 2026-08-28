namespace PqrsPlatform.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador público que viaja en data-tenant y en X-Tenant-Id.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Dominios autorizados a incrustar el widget. Alimenta el CORS dinámico.</summary>
    public List<string> AllowedOrigins { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<KnowledgeBaseArticle> Articles { get; set; } = new List<KnowledgeBaseArticle>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
