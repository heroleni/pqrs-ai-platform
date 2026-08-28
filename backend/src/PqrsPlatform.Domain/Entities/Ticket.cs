using PqrsPlatform.Domain.Enums;

namespace PqrsPlatform.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string TicketNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Nulos a propósito: si el LLM falla, el ticket se guarda sin clasificar.
    public TicketType? Type { get; set; }
    public TicketPriority? Priority { get; set; }
    public Sentiment? Sentiment { get; set; }
    public string? Summary { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Pendiente;

    public bool CameFromRag { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
