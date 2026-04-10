using Refit;
namespace MailAgent.Application.Ollama;

internal interface IOllamaApi
{
  [Post("/api/generate")]
  Task<OllamaGenerateResponse> Generate(OllamaGenerateRequest request, CancellationToken cancellationToken);
}
