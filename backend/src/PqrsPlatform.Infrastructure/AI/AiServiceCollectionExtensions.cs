using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public enum AiProvider { Local, OpenAi, Gemini }

public static class AiServiceCollectionExtensions
{
    public static AiProvider ResolveProvider(IConfiguration cfg)
    {
        var explicitChoice = cfg["AI_PROVIDER"]?.Trim();

        if (!string.IsNullOrWhiteSpace(explicitChoice) &&
            Enum.TryParse<AiProvider>(explicitChoice, ignoreCase: true, out var chosen))
            return chosen;

        if (!string.IsNullOrWhiteSpace(cfg["OPENAI_API_KEY"])) return AiProvider.OpenAi;
        if (!string.IsNullOrWhiteSpace(cfg["GEMINI_API_KEY"])) return AiProvider.Gemini;

        return AiProvider.Local;
    }

    public static IServiceCollection AddAiServices(
        this IServiceCollection services, IConfiguration cfg, out AiProvider provider)
    {
        provider = ResolveProvider(cfg);

        switch (provider)
        {
            case AiProvider.OpenAi:
                services.AddHttpClient<OpenAiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
                services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
                services.AddScoped<ILlmService, OpenAiLlmService>();
                break;

            case AiProvider.Gemini:
                services.AddHttpClient<GeminiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
                services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
                services.AddScoped<ILlmService, GeminiLlmService>();
                break;

            default:
                services.AddScoped<IEmbeddingService, LocalEmbeddingService>();
                services.AddScoped<ILlmService, LocalLlmService>();
                break;
        }

        return services;
    }
}
