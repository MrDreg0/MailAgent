using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;

namespace MailAgent.Application.LmStudio;

internal sealed class LmStudioLlmClient(
  ILmStudioApi lmStudioApi,
  ILogger<LmStudioLlmClient> logger) : ILlmClient
{
  private const int IdleTtlSeconds = 5;

  public async Task<LlmGenerateResponse> Generate(LlmGenerateRequest request, CancellationToken cancellationToken)
  {
    using var response = await lmStudioApi.CreateChatCompletion(
      new LmStudioChatCompletionRequest(
        request.Model,
        [new LmStudioChatMessage("user", request.Prompt)],
        Stream: false,
        Ttl: IdleTtlSeconds),
      cancellationToken);

    if (!response.IsSuccessful)
    {
      logger.LogError(
        response.Error,
        "LM Studio request failed. StatusCode: {StatusCode}. Model: {Model}. Prompt length: {PromptLength}. Response: {Response}",
        response.StatusCode,
        request.Model,
        request.Prompt.Length,
        response.Error?.Content);

      await response.EnsureSuccessfulAsync();
    }

    var content = response.Content?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;

    return new LlmGenerateResponse(content);
  }
}
