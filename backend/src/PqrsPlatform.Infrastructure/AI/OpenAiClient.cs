using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PqrsPlatform.Infrastructure.AI;

public class OpenAiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public string EmbeddingModel => _config["EMBEDDING_MODEL"] ?? "text-embedding-3-small";
    public string ChatModel => _config["LLM_MODEL"] ?? "gpt-4o-mini";

    public OpenAiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;

        var apiKey = _config["OPENAI_API_KEY"];
        _http.BaseAddress ??= new Uri("https://api.openai.com/v1/");
        if (!string.IsNullOrWhiteSpace(apiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("embeddings", new { model = EmbeddingModel, input }, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var vector = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return vector.EnumerateArray().Select(v => v.GetSingle()).ToArray();
    }

    public async Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var body = new
        {
            model = ChatModel,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2
        };

        var resp = await _http.PostAsJsonAsync("chat/completions", body, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
