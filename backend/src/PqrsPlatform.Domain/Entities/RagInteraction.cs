namespace PqrsPlatform.Domain.Entities;
public class RagInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Query { get; set; } = string.Empty;
    
    public double TopScore { get; set; }
    
    public bool Answered { get; set; }
    public string? Answer { get; set; }
    
    public bool? ResolvedByUser { get; set; }
    
    public Guid? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}