using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PqrsPlatform.Infrastructure.AI;

public class GeminiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public string EmbeddingModel => _config["GEMINI_EMBEDDING_MODEL"] ?? "text-embedding-004";
    public string ChatModel => _config["GEMINI_LLM_MODEL"] ?? "gemini-2.0-flash";

    private string ApiKey => _config["GEMINI_API_KEY"]
        ?? throw new InvalidOperationException("Falta GEMINI_API_KEY.");

    public GeminiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        _http.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
    }

    public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken ct)
    {
        var body = new
        {
            model = $"models/{EmbeddingModel}",
            content = new { parts = new[] { new { text = input } } }
        };

        var url = $"models/{EmbeddingModel}:embedContent?key={ApiKey}";

        var resp = await _http.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        var values = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values")
            .EnumerateArray()
            .Select(v => v.GetSingle())
            .ToArray();

        return EmbeddingMath.Fit(values);
    }

    public async Task<string> CreateChatCompletionAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new { temperature = 0.2 }
        };

        var url = $"models/{ChatModel}:generateContent?key={ApiKey}";

        var resp = await _http.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0) return string.Empty;

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        if (parts.GetArrayLength() == 0) return string.Empty;

        return parts[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
