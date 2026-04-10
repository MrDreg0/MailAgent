using System.Text.Json.Serialization;
namespace MailAgent.Application.LmStudio;

internal sealed record LmStudioChatCompletionChoice(
  [property: JsonPropertyName("message")] LmStudioChatMessage Message);
