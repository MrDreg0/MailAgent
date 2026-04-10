using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Llm;
namespace MailAgent.Application.Ollama;

internal sealed class OllamaLlmClient(IOllamaApi ollamaApi) : ILlmClient
{
  public async Task<LlmGenerateResponse> Generate(LlmGenerateRequest request, CancellationToken cancellationToken)
  {
    var response = await ollamaApi.Generate(
      new OllamaGenerateRequest(request.Model, request.Prompt, Stream: false, KeepAlive: 0),
      cancellationToken);

    return new LlmGenerateResponse(response.Response);
  }
}
