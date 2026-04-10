using System.Text.Json;
using MailAgent.Application.Ollama;

namespace MailAgent.Application.Tests;

[TestFixture]
public class OllamaGenerateRequestTests
{
  [Test]
  public void Serialize_UsesSnakeCaseKeepAliveProperty()
  {
    // Given.
    var request = new OllamaGenerateRequest(
      Model: "model",
      Prompt: "prompt",
      Stream: false,
      KeepAlive: 0);

    // When.
    var json = JsonSerializer.Serialize(request);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(json, Does.Contain("\"keep_alive\""));
      Assert.That(json, Does.Not.Contain("\"KeepAlive\""));
      Assert.That(json, Does.Not.Contain("\"keepAlive\""));
    });
  }
}
