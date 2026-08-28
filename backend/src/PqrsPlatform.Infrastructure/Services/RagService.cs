using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;
using PqrsPlatform.Application.DTOs;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Interfaces;
using PqrsPlatform.Infrastructure.Persistence;

namespace PqrsPlatform.Infrastructure.Services;

public class RagService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddings;
    private readonly ILlmService _llm;
    private readonly ITenantContext _tenant;
    private readonly double _threshold;
    private readonly int _topK;

    public RagService(
        AppDbContext db,
        IEmbeddingService embeddings,
        ILlmService llm,
        ITenantContext tenant,
        IConfiguration config)
    {
        _db = db;
        _embeddings = embeddings;
        _llm = llm;
        _tenant = tenant;
        // InvariantCulture obligatorio: en un contenedor con locale es_CO,
        // double.TryParse("0.35") con la cultura actual interpreta el punto
        // como separador de miles y devuelve 35, lo que apaga el RAG.
        _threshold = double.TryParse(
            config["RAG_SIMILARITY_THRESHOLD"] ?? config["Rag:SimilarityThreshold"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var t) ? t : 0.75;
        _topK = int.TryParse(config["RAG_TOP_K"] ?? config["Rag:TopK"], out var k) ? k : 3;
    }

    public async Task<RagSearchResponse> SearchAsync(string query, CancellationToken ct = default)
    {
        var queryVector = await _embeddings.EmbedAsync(query, ct);
        
        var matches = await _db.KnowledgeBaseArticles
            .Where(a => a.Embedding != null)
            .OrderBy(a => a.Embedding!.CosineDistance(queryVector))
            .Take(_topK)
            .Select(a => new
            {
                a.Content,
                a.Title,
                Distance = a.Embedding!.CosineDistance(queryVector)
            })
            .ToListAsync(ct);

        var topScore = matches.Count > 0 ? 1 - matches[0].Distance : 0d;
        var interaction = new RagInteraction
        {
            TenantId = _tenant.TenantId,
            Query = query,
            TopScore = topScore
        };

        if (topScore >= _threshold && matches.Count > 0)
        {
            var context = matches.Select(m => $"{m.Title}: {m.Content}").ToList();
            var answer = await _llm.AnswerFromContextAsync(query, context, ct);

            interaction.Answered = true;
            interaction.Answer = answer;

            _db.RagInteractions.Add(interaction);
            await _db.SaveChangesAsync(ct);

            return new RagSearchResponse(interaction.Id, true, answer, topScore);
        }

        interaction.Answered = false;
        _db.RagInteractions.Add(interaction);
        await _db.SaveChangesAsync(ct);

        return new RagSearchResponse(interaction.Id, false, null, topScore);
    }
    
    public async Task<bool> RegisterFeedbackAsync(Guid interactionId, bool resolvedByUser, CancellationToken ct = default)
    {
        var interaction = await _db.RagInteractions.FirstOrDefaultAsync(i => i.Id == interactionId, ct);
        if (interaction is null) return false;

        interaction.ResolvedByUser = resolvedByUser;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
