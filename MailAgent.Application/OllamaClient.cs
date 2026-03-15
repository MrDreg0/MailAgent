using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailAgent.Application;

public sealed class OllamaClient(IHttpClientFactory httpClientFactory)
{
  public async Task<string> GenerateAsync(string model, string prompt, CancellationToken cancellationToken = default)
  {
    var request = new OllamaGenerateRequest(
      model,
      prompt,
      false,
      0);

    var json = JsonSerializer.Serialize(request);
    using var content = new StringContent(json, Encoding.UTF8, "application/json");

    var httpClient = httpClientFactory.CreateClient("ollama");
    using var response = await httpClient.PostAsync("api/generate", content, cancellationToken);
    response.EnsureSuccessStatusCode();

    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
    var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

    return parsed?.Response ?? string.Empty;
  }
}

file sealed record OllamaGenerateRequest(
  [property: JsonPropertyName("model")] string Model,
  [property: JsonPropertyName("prompt")] string Prompt,
  [property: JsonPropertyName("stream")] bool Stream,
  [property: JsonPropertyName("keep_alive")] int KeepAlive);

file sealed record OllamaGenerateResponse(
  [property: JsonPropertyName("response")] string Response);
