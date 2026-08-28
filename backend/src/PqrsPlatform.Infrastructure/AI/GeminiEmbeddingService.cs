using Pgvector;
using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly GeminiClient _client;

    public GeminiEmbeddingService(GeminiClient client) => _client = client;

    public async Task<Vector> EmbedAsync(string text, CancellationToken ct = default)
        => new(await _client.CreateEmbeddingAsync(text, ct));
}
