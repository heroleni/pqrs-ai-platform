using PqrsPlatform.Domain.Entities;

namespace PqrsPlatform.Application.Interfaces;

/// <summary>
/// Envía eventos en tiempo real cuando un ticket entra con prioridad alta o sentimiento negativo.
/// Implementado en la capa Api con SignalR (ver PqrsPlatform.Api.Hubs.SignalRTicketNotifier).
/// </summary>
public interface ITicketNotifier
{
    Task NotifyCriticalTicketAsync(Ticket ticket, CancellationToken ct = default);
}
