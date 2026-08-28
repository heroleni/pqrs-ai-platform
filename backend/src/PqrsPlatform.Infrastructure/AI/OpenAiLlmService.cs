using Microsoft.Extensions.Logging;
using PqrsPlatform.Application.Interfaces;
using PqrsPlatform.Infrastructure.Prompts;

namespace PqrsPlatform.Infrastructure.AI;

public class OpenAiLlmService : ILlmService
{
    private readonly OpenAiClient _client;
    private readonly ILogger<OpenAiLlmService> _logger;

    public OpenAiLlmService(OpenAiClient client, ILogger<OpenAiLlmService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task<string> AnswerFromContextAsync(
        string question, IReadOnlyList<string> contextArticles, CancellationToken ct = default)
        => _client.CreateChatCompletionAsync(
            PromptTemplates.RagSystemPrompt,
            PromptTemplates.BuildRagUserPrompt(question, contextArticles),
            ct);

    public async Task<TriageResult?> TriageTicketAsync(
        string subject, string description, CancellationToken ct = default)
    {
        var raw = await _client.CreateChatCompletionAsync(
            PromptTemplates.TriageSystemPrompt,
            PromptTemplates.BuildTriageUserPrompt(subject, description),
            ct);

        try
        {
            return AiJson.ParseTriage(raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo parsear el triaje del LLM: {Raw}", raw);
            return null;
        }
    }
}
