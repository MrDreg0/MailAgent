using Refit;
namespace MailAgent.Application.Ollama;

internal interface IOllamaApi
{
  [Post("/api/generate")]
  Task<ApiResponse<OllamaGenerateResponse>> Generate(OllamaGenerateRequest request, CancellationToken cancellationToken);
}
