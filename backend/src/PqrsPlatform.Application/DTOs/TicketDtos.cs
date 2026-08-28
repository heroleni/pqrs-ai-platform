using PqrsPlatform.Domain.Enums;

namespace PqrsPlatform.Application.DTOs;

public record TicketResponse(
    Guid Id,
    string TicketNumber,
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Description,
    TicketType? Type,
    TicketPriority? Priority,
    Sentiment? Sentiment,
    string? Summary,
    TicketStatus Status,
    bool CameFromRag,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record UpdateTicketStatusRequest(TicketStatus Status);

public record TicketListQuery(TicketStatus? Status, TicketPriority? Priority, int Page = 1, int PageSize = 20);
