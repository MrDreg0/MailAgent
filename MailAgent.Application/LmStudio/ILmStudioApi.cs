using Refit;
namespace MailAgent.Application.LmStudio;

internal interface ILmStudioApi
{
  [Post("/v1/chat/completions")]
  Task<ApiResponse<LmStudioChatCompletionResponse>> CreateChatCompletion(LmStudioChatCompletionRequest request, CancellationToken cancellationToken);
}
