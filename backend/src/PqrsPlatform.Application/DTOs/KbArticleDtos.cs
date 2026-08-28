namespace PqrsPlatform.Application.DTOs;

public record CreateKbArticleRequest(string Title, string Content);

public record UpdateKbArticleRequest(string Title, string Content);

public record KbArticleResponse(
    Guid Id,
    string Title,
    string Content,
    bool HasEmbedding,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
