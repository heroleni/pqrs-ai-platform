using Pgvector;
using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly OpenAiClient _client;

    public OpenAiEmbeddingService(OpenAiClient client) => _client = client;

    public async Task<Vector> EmbedAsync(string text, CancellationToken ct = default)
    {
        var values = await _client.CreateEmbeddingAsync(text, ct);
        return new Vector(EmbeddingMath.Fit(values));
    }
}
