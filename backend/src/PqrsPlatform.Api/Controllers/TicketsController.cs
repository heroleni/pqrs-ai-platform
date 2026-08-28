using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsPlatform.Application.DTOs;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Enums;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Api.Controllers;

/// <summary>Gestión de PQRS para agentes: listar, filtrar por estado/prioridad y actualizar ciclo de vida.</summary>
[ApiController]
[Authorize]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<TicketResponse>>> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.Tickets.AsQueryable();
        if (status is not null) query = query.Where(t => t.Status == status);
        if (priority is not null) query = query.Where(t => t.Priority == priority);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return Ok(tickets.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Get(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        return ticket is null ? NotFound() : Ok(ToResponse(ticket));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketResponse>> UpdateStatus(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return NotFound();

        ticket.Status = request.Status;
        if (request.Status == TicketStatus.Resuelto)
            ticket.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(ticket));
    }

    private static TicketResponse ToResponse(Ticket t) => new(
        t.Id, t.TicketNumber, t.CustomerName, t.CustomerEmail, t.Subject, t.Description,
        t.Type, t.Priority, t.Sentiment, t.Summary, t.Status, t.CameFromRag, t.CreatedAt, t.ResolvedAt);
}
