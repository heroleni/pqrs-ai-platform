namespace PqrsPlatform.Application.DTOs;

public record RagSearchRequest(string Query);

public record RagSearchResponse(
    Guid InteractionId,
    bool Answered,
    string? Answer,
    double TopScore
);

public record RagFeedbackRequest(Guid InteractionId, bool ResolvedByUser);

public record CreateWidgetTicketRequest(
    string CustomerName,
    string CustomerEmail,
    string Subject,
    string Description,
    Guid? RagInteractionId
);

public record CreateWidgetTicketResponse(
    string TicketNumber,
    Guid TicketId,
    string Status,
    string? Type,
    string? Priority
);