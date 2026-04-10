using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Logging;
using Refit;
namespace MailAgent.Application.LmStudio;

internal sealed class LmStudioLlmClient(
  ILmStudioApi lmStudioApi,
  ILogger<LmStudioLlmClient> logger) : ILlmClient
{
  private const int IdleTtlSeconds = 5;

  public async Task<LlmGenerateResponse> Generate(LlmGenerateRequest request, CancellationToken cancellationToken)
  {
    LmStudioChatCompletionResponse response;

    try
    {
      response = await lmStudioApi.CreateChatCompletion(
        new LmStudioChatCompletionRequest(
          request.Model,
          [new LmStudioChatMessage("user", request.Prompt)],
          Stream: false,
          Ttl: IdleTtlSeconds),
        cancellationToken);
    }
    catch (ApiException exception)
    {
      logger.LogError(
        exception,
        "LM Studio request failed. StatusCode: {StatusCode}. Model: {Model}. Prompt length: {PromptLength}. Response: {Response}",
        (int)exception.StatusCode,
        request.Model,
        request.Prompt.Length,
        exception.Content);

      throw;
    }

    var content = response.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;

    return new LlmGenerateResponse(content);
  }
}
