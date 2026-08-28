using Microsoft.AspNetCore.Mvc;
using PqrsPlatform.Application.DTOs;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Interfaces;
using PqrsPlatform.Infrastructure.Persistence;
using PqrsPlatform.Infrastructure.Services;

namespace PqrsPlatform.Api.Controllers;

/// <summary>
/// Endpoints públicos consumidos por widget/pqrs-widget.js.
/// Requieren el header X-Tenant-Id (resuelto por TenantResolutionMiddleware).
/// </summary>
[ApiController]
[Route("api/v1/widget")]
public class WidgetController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly RagService _rag;
    private readonly TicketTriageService _triage;

    public WidgetController(AppDbContext db, ITenantContext tenant, RagService rag, TicketTriageService triage)
    {
        _db = db;
        _tenant = tenant;
        _rag = rag;
        _triage = triage;
    }

    private ActionResult? RequireTenant()
        => _tenant.IsResolved ? null : Unauthorized(new { error = "Tenant no resuelto. Envía el header X-Tenant-Id." });

    [HttpPost("rag-search")]
    public async Task<ActionResult<RagSearchResponse>> RagSearch(RagSearchRequest request, CancellationToken ct)
    {
        if (RequireTenant() is { } denied) return denied;
        if (string.IsNullOrWhiteSpace(request.Query)) return BadRequest(new { error = "La consulta no puede estar vacía." });

        var result = await _rag.SearchAsync(request.Query, ct);
        return Ok(result);
    }

    [HttpPost("rag-feedback")]
    public async Task<IActionResult> RagFeedback(RagFeedbackRequest request, CancellationToken ct)
    {
        if (RequireTenant() is { } denied) return denied;

        var ok = await _rag.RegisterFeedbackAsync(request.InteractionId, request.ResolvedByUser, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<CreateWidgetTicketResponse>> CreateTicket(CreateWidgetTicketRequest request, CancellationToken ct)
    {
        if (RequireTenant() is { } denied) return denied;

        if (string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.CustomerEmail) ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "Nombre, correo, asunto y descripción son obligatorios." });
        }

        var ticket = new Ticket
        {
            TenantId = _tenant.TenantId,
            TicketNumber = GenerateTicketNumber(),
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            Subject = request.Subject,
            Description = request.Description,
            CameFromRag = request.RagInteractionId.HasValue
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        if (request.RagInteractionId is { } interactionId)
        {
            var interaction = await _db.RagInteractions.FindAsync(new object?[] { interactionId }, ct);
            if (interaction is not null)
            {
                interaction.TicketId = ticket.Id;
                interaction.ResolvedByUser = false;
                await _db.SaveChangesAsync(ct);
            }
        }

        // Triaje asíncrono no bloqueante: el ticket ya quedó radicado aunque la IA falle o tarde.
        await _triage.ClassifyAsync(ticket, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(new CreateWidgetTicketResponse(
            ticket.TicketNumber,
            ticket.Id,
            ticket.Status.ToString(),
            ticket.Type?.ToString(),
            ticket.Priority?.ToString()));
    }

    private static string GenerateTicketNumber()
        => $"PQRS-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
}
