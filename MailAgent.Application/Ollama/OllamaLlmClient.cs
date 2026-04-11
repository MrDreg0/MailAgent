using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;
using Refit;

namespace MailAgent.Application.Ollama;

internal sealed class OllamaLlmClient(
  IOllamaApi ollamaApi,
  ILogger<OllamaLlmClient> logger) : ILlmClient
{
  public async Task<LlmGenerateResponse> Generate(LlmGenerateRequest request, CancellationToken cancellationToken)
  {
    using var response = await ollamaApi.Generate(
      new OllamaGenerateRequest(request.Model, request.Prompt, Stream: false, KeepAlive: 0),
      cancellationToken);

    if (!response.IsSuccessful)
    {
      logger.LogError(
        response.Error,
        "Ollama request failed. StatusCode: {StatusCode}. Model: {Model}. Prompt length: {PromptLength}. Response: {Response}",
        response.StatusCode,
        request.Model,
        request.Prompt.Length,
        response.Error?.Content);

      await response.EnsureSuccessfulAsync();
    }

    return new LlmGenerateResponse(response.Content?.Response ?? string.Empty);
  }
}
