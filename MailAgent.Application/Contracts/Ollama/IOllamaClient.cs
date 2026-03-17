using MailAgent.Application.Ollama;
using Refit;
namespace MailAgent.Application.Contracts.Ollama;

public interface IOllamaClient
{
  [Post("/api/generate")]
  Task<OllamaGenerateResponse> Generate(OllamaGenerateRequest request, CancellationToken cancellationToken);
}
