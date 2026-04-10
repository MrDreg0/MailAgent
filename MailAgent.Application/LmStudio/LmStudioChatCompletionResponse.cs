using System.Text.Json.Serialization;
namespace MailAgent.Application.LmStudio;

internal sealed record LmStudioChatCompletionResponse(
  [property: JsonPropertyName("choices")] IReadOnlyList<LmStudioChatCompletionChoice> Choices);
