using System.Text.Json;
using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public static class AiJson
{
    private static readonly string[] ValidTypes = { "Peticion", "Queja", "Reclamo", "Sugerencia" };
    private static readonly string[] ValidPriorities = { "Baja", "Media", "Alta" };
    private static readonly string[] ValidSentiments = { "Positivo", "Neutro", "Negativo" };

    public static string Strip(string raw)
    {
        var text = raw.Trim();

        if (text.StartsWith("```"))
        {
            var firstBreak = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstBreak >= 0 && lastFence > firstBreak)
                text = text[(firstBreak + 1)..lastFence].Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    public static TriageResult? ParseTriage(string raw)
    {
        using var doc = JsonDocument.Parse(Strip(raw));
        var root = doc.RootElement;

        var type = Normalize(Read(root, "type", "tipo"), ValidTypes, "Peticion");
        var priority = Normalize(Read(root, "priority", "prioridad"), ValidPriorities, "Media");
        var sentiment = Normalize(Read(root, "sentiment", "sentimiento"), ValidSentiments, "Neutro");
        var summary = Read(root, "summary", "resumen") ?? string.Empty;

        if (summary.Length > 500) summary = summary[..500];

        return new TriageResult(type, priority, sentiment, summary);
    }

    private static string? Read(JsonElement root, params string[] names)
    {
        foreach (var name in names)
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();

        return null;
    }

    private static string Normalize(string? value, string[] allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        var match = allowed.FirstOrDefault(a =>
            string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));

        return match ?? fallback;
    }
}
