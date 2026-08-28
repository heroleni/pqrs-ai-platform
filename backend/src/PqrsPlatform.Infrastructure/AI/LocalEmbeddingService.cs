using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pgvector;
using PqrsPlatform.Application.Interfaces;

namespace PqrsPlatform.Infrastructure.AI;

public class LocalEmbeddingService : IEmbeddingService
{
    private static readonly char[] Separators =
        " \t\n\r.,;:¿?¡!()[]{}\"'/\\-_".ToCharArray();

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "el","la","los","las","un","una","unos","unas","de","del","al","en","y","o","que",
        "por","para","con","sin","es","son","mi","su","se","lo","como","cuando","donde","cual",
        "muy","mas","ya","he","ha","hay","este","esta","esto","the","of","and","puedo","quiero"
    };

    public Task<Vector> EmbedAsync(string text, CancellationToken ct = default)
    {
        var vector = new float[EmbeddingMath.TargetDimensions];

        foreach (var token in Tokenize(text))
        {
            vector[Bucket(token)] += 1f;

            if (token.Length > 4)
                vector[Bucket(token[..^1])] += 0.6f;

            if (token.Length > 6)
                vector[Bucket(token[..^2])] += 0.3f;
        }

        EmbeddingMath.Normalize(vector);
        return Task.FromResult(new Vector(vector));
    }

    private static IEnumerable<string> Tokenize(string text) =>
        RemoveAccents(text.ToLowerInvariant())
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !StopWords.Contains(t));

    private static int Bucket(string token)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(token));
        return (int)(BitConverter.ToUInt32(bytes, 0) % EmbeddingMath.TargetDimensions);
    }

    private static string RemoveAccents(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
