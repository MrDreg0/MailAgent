using System.Text.Json.Serialization;
namespace MailAgent.Application.LmStudio;

internal sealed record LmStudioChatCompletionRequest(
  [property: JsonPropertyName("model")] string Model,
  [property: JsonPropertyName("messages")] IReadOnlyList<LmStudioChatMessage> Messages,
  [property: JsonPropertyName("stream")] bool Stream,
  [property: JsonPropertyName("ttl")] int Ttl);
