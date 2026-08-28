using Pgvector;

namespace PqrsPlatform.Application.Interfaces;

public interface IEmbeddingService
{
    /// <summary>Genera el vector de embedding (1536 dim) para un texto dado.</summary>
    Task<Vector> EmbedAsync(string text, CancellationToken ct = default);
}
