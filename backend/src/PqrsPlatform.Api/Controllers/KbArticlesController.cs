using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsPlatform.Application.DTOs;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Api.Controllers;

/// <summary>CRUD de artículos de conocimiento, con generación automática de embeddings al guardar.</summary>
[ApiController]
[Authorize]
[Route("api/v1/kb-articles")]
public class KbArticlesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddings;

    public KbArticlesController(AppDbContext db, IEmbeddingService embeddings)
    {
        _db = db;
        _embeddings = embeddings;
    }

    [HttpGet]
    public async Task<ActionResult<List<KbArticleResponse>>> List(CancellationToken ct)
    {
        var articles = await _db.KnowledgeBaseArticles.OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);
        return Ok(articles.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KbArticleResponse>> Get(Guid id, CancellationToken ct)
    {
        var article = await _db.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        return article is null ? NotFound() : Ok(ToResponse(article));
    }

    [HttpPost]
    public async Task<ActionResult<KbArticleResponse>> Create(CreateKbArticleRequest request, CancellationToken ct)
    {
        var article = new KnowledgeBaseArticle { Title = request.Title, Content = request.Content };
        article.Embedding = await TryEmbedAsync(article.Title, article.Content, ct);

        _db.KnowledgeBaseArticles.Add(article);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = article.Id }, ToResponse(article));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KbArticleResponse>> Update(Guid id, UpdateKbArticleRequest request, CancellationToken ct)
    {
        var article = await _db.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return NotFound();

        article.Title = request.Title;
        article.Content = request.Content;
        article.UpdatedAt = DateTime.UtcNow;
        article.Embedding = await TryEmbedAsync(article.Title, article.Content, ct);

        await _db.SaveChangesAsync(ct);
        return Ok(ToResponse(article));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var article = await _db.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return NotFound();

        _db.KnowledgeBaseArticles.Remove(article);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Pgvector.Vector?> TryEmbedAsync(string title, string content, CancellationToken ct)
    {
        try
        {
            return await _embeddings.EmbedAsync($"{title}: {content}", ct);
        }
        catch
        {
            // El artículo se guarda igual; sin embedding simplemente no participa en la búsqueda RAG
            // hasta que se reintente (p.ej. guardándolo de nuevo).
            return null;
        }
    }

    private static KbArticleResponse ToResponse(KnowledgeBaseArticle a)
        => new(a.Id, a.Title, a.Content, a.Embedding is not null, a.CreatedAt, a.UpdatedAt);
}
