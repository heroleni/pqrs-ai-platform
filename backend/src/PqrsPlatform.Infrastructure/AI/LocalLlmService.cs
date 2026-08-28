using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public class LocalLlmService : ILlmService
{
    public Task<string> AnswerFromContextAsync(
        string question, IReadOnlyList<string> contextArticles, CancellationToken ct = default)
    {
        if (contextArticles.Count == 0)
            return Task.FromResult(
                "No encontré esa información en nuestra base de conocimiento. " +
                "Te sugiero radicar tu solicitud para que un agente la revise.");

        var best = contextArticles[0].Trim();

        var body = best.Contains('\n')
            ? best[(best.IndexOf('\n') + 1)..].Trim()
            : best;

        return Task.FromResult(Shorten(body, 3));
    }

    public Task<TriageResult?> TriageTicketAsync(
        string subject, string description, CancellationToken ct = default)
    {
        var text = $"{subject} {description}".ToLowerInvariant();

        var type = "Peticion";
        if (Has(text, "sugiero", "sugerencia", "propongo", "recomiendo", "seria bueno", "deberian"))
            type = "Sugerencia";
        else if (Has(text, "cobro", "cobraron", "factura", "fuga", "dano", "falla", "no funciona",
                     "sin servicio", "exijo", "reclamo", "devolucion", "incumpl"))
            type = "Reclamo";
        else if (Has(text, "queja", "mala atencion", "grosero", "maltrato", "pesimo",
                     "inaceptable", "nadie me responde", "nadie responde"))
            type = "Queja";

        var priority = type switch
        {
            "Sugerencia" => "Baja",
            "Reclamo" => "Alta",
            "Queja" => "Alta",
            _ => "Media"
        };

        if (Has(text, "urgente", "emergencia", "peligro", "riesgo", "salud", "inundac",
                "tercera vez", "hace semanas", "sigue igual"))
            priority = "Alta";

        var sentiment = "Neutro";
        if (Has(text, "gracias", "excelente", "felicit", "agradezco", "muy buen"))
            sentiment = "Positivo";
        else if (Has(text, "molesto", "inaceptable", "pesimo", "indignado", "harto", "terrible",
                     "exijo", "grosero", "maltrato", "nadie responde", "cansado"))
            sentiment = "Negativo";

        var summary = string.IsNullOrWhiteSpace(subject)
            ? Shorten(description, 1)
            : $"{subject.Trim()}. Clasificada como {type.ToLowerInvariant()} de prioridad {priority.ToLowerInvariant()}.";

        return Task.FromResult<TriageResult?>(new TriageResult(type, priority, sentiment, summary));
    }

    private static bool Has(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static string Shorten(string text, int maxSentences)
    {
        var clean = text.Trim();
        
        if (clean.Length <= 400) return clean;

        var sentences = clean
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(maxSentences)
            .ToList();

        return sentences.Count == 0 ? clean : string.Join(". ", sentences) + ".";
    }
}