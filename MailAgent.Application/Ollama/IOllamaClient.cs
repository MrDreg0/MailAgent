using Refit;
namespace MailAgent.Application.Ollama;

public interface IOllamaClient
{
  [Post("/api/generate")]
  Task<OllamaGenerateResponse> Generate(OllamaGenerateRequest request, CancellationToken cancellationToken);
}
