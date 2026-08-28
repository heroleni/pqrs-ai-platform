using Microsoft.AspNetCore.SignalR;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;

namespace PqrsPlatform.Api.Hubs;
public class SignalRTicketNotifier : ITicketNotifier
{
    private readonly IHubContext<TicketsHub> _hub;

    public SignalRTicketNotifier(IHubContext<TicketsHub> hub) => _hub = hub;

    public Task NotifyCriticalTicketAsync(Ticket ticket, CancellationToken ct = default)
        => _hub.Clients.Group(TicketsHub.GroupName(ticket.TenantId.ToString()))
            .SendAsync("CriticalTicket", new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.Subject,
                ticket.CustomerName,
                ticket.Summary,
                Type = ticket.Type?.ToString(),
                Priority = ticket.Priority?.ToString(),
                Sentiment = ticket.Sentiment?.ToString(),
                ticket.CreatedAt
            }, ct);
}