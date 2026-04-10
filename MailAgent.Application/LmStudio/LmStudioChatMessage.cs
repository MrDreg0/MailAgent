using System.Text.Json.Serialization;
namespace MailAgent.Application.LmStudio;

internal sealed record LmStudioChatMessage(
  [property: JsonPropertyName("role")] string Role,
  [property: JsonPropertyName("content")] string Content);
