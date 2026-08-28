namespace PqrsPlatform.Application.Interfaces;

public record TriageResult(string Type, string Priority, string Sentiment, string Summary);

public interface ILlmService
{
    /// <summary>Sintetiza una respuesta directa basada exclusivamente en los artículos recuperados (RAG).</summary>
    Task<string> AnswerFromContextAsync(string question, IReadOnlyList<string> contextArticles, CancellationToken ct = default);

    /// <summary>Clasifica un ticket (Tipo, Prioridad, Sentimiento, Resumen) a partir de asunto + descripción.</summary>
    Task<TriageResult?> TriageTicketAsync(string subject, string description, CancellationToken ct = default);
}
