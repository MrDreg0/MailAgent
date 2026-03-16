using System.Text.Json.Serialization;
namespace MailAgent.Application.Ollama;

public sealed record OllamaGenerateRequest(
  string Model,
  string Prompt,
  bool Stream,
  [property: JsonPropertyName("keep_alive")] int KeepAlive);
