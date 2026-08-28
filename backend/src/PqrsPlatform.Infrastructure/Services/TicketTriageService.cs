using Microsoft.Extensions.Logging;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Enums;

namespace PqrsPlatform.Infrastructure.Services;

public class TicketTriageService
{
    private readonly ILlmService _llm;
    private readonly ITicketNotifier _notifier;
    private readonly ILogger<TicketTriageService> _logger;

    public TicketTriageService(ILlmService llm, ITicketNotifier notifier, ILogger<TicketTriageService> logger)
    {
        _llm = llm;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task ClassifyAsync(Ticket ticket, CancellationToken ct = default)
    {
        try
        {
            var result = await _llm.TriageTicketAsync(ticket.Subject, ticket.Description, ct);
            if (result is null) return;

            if (Enum.TryParse<TicketType>(result.Type, true, out var type)) ticket.Type = type;
            if (Enum.TryParse<TicketPriority>(result.Priority, true, out var priority)) ticket.Priority = priority;
            if (Enum.TryParse<Sentiment>(result.Sentiment, true, out var sentiment)) ticket.Sentiment = sentiment;
            ticket.Summary = result.Summary;

            var isCritical = ticket.Priority == TicketPriority.Alta || ticket.Sentiment == Sentiment.Negativo;
            if (isCritical)
                await _notifier.NotifyCriticalTicketAsync(ticket, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El triaje por IA falló para el ticket {TicketNumber}; se guarda sin clasificar.", ticket.TicketNumber);
        }
    }
}
