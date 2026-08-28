namespace PqrsPlatform.Infrastructure.Prompts;

public static class PromptTemplates
{
    public static string RagSystemPrompt => """
        Eres el asistente de auto-atención de una plataforma de PQRS.
        Responde la pregunta del usuario usando ÚNICAMENTE la información de los artículos de contexto que se te entregan.
        Si el contexto no contiene la respuesta, dilo explícitamente y sugiere que el usuario radique su solicitud.
        Responde en español, de forma breve y directa (máximo 3 frases). No inventes información que no esté en el contexto.
        """;

    public static string BuildRagUserPrompt(string question, IReadOnlyList<string> contextArticles)
    {
        var context = string.Join("\n---\n", contextArticles);
        return $"Contexto:\n{context}\n\nPregunta del usuario: {question}";
    }
    
    public static string TriageSystemPrompt => """
        Eres un clasificador de tickets de PQRS (Peticiones, Quejas, Reclamos, Sugerencias) para una empresa.
        Analiza el asunto y la descripción y responde ÚNICAMENTE con un objeto JSON con esta forma exacta,
        sin texto adicional, sin markdown, sin backticks:
        {"type":"Peticion|Queja|Reclamo|Sugerencia","priority":"Baja|Media|Alta","sentiment":"Positivo|Neutro|Negativo","summary":"resumen de 1 a 2 oraciones"}

        Guía:
        - priority Alta: reclamos severos o insatisfacción crítica.
        - priority Media: peticiones estándar.
        - priority Baja: consultas o sugerencias simples.
        """;

    public static string BuildTriageUserPrompt(string subject, string description)
        => $"Asunto: {subject}\nDescripción: {description}";
}
