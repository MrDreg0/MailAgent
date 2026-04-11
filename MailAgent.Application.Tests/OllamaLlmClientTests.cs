using System.Net;
using MailAgent.Application.Llm;
using MailAgent.Application.Ollama;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace MailAgent.Application.Tests;

[TestFixture]
public class OllamaLlmClientTests
{
  [Test]
  public async Task Generate_SendsRequestAndReturnsResponseText()
  {
    // Given.
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var api = new FakeOllamaApi(CreateSuccessResponse(new OllamaGenerateResponse("result text")));
    var sut = new OllamaLlmClient(api, NullLogger<OllamaLlmClient>.Instance);

    // When.
    var result = await sut.Generate(new LlmGenerateRequest("local-model", "prompt text"), cancellationToken);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Response, Is.EqualTo("result text"));
      Assert.That(api.Request, Is.Not.Null);
      Assert.That(api.Request!.Model, Is.EqualTo("local-model"));
      Assert.That(api.Request.Prompt, Is.EqualTo("prompt text"));
      Assert.That(api.Request.Stream, Is.False);
      Assert.That(api.Request.KeepAlive, Is.EqualTo(0));
      Assert.That(api.CancellationToken, Is.EqualTo(cancellationToken));
    });
  }

  [Test]
  public void Generate_ThrowsApiException_WhenOllamaReturnsUnsuccessfulResponse()
  {
    // Given.
    var api = new FakeOllamaApi(CreateFailureResponse(HttpStatusCode.BadRequest, "bad request"));
    var sut = new OllamaLlmClient(api, NullLogger<OllamaLlmClient>.Instance);

    // When.
    var act = async () => await sut.Generate(new LlmGenerateRequest("local-model", "prompt text"), CancellationToken.None);

    // Then.
    Assert.That(act, Throws.TypeOf<ApiException>());
  }

  private sealed class FakeOllamaApi(ApiResponse<OllamaGenerateResponse> response) : IOllamaApi
  {
    public OllamaGenerateRequest? Request { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    public Task<ApiResponse<OllamaGenerateResponse>> Generate(OllamaGenerateRequest request, CancellationToken cancellationToken)
    {
      Request = request;
      CancellationToken = cancellationToken;

      return Task.FromResult(response);
    }
  }

  private static ApiResponse<OllamaGenerateResponse> CreateSuccessResponse(OllamaGenerateResponse content)
  {
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/generate"),
    };

    return new ApiResponse<OllamaGenerateResponse>(httpResponse, content, new RefitSettings(), null);
  }

  private static ApiResponse<OllamaGenerateResponse> CreateFailureResponse(HttpStatusCode statusCode, string responseContent)
  {
    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/generate");
    var httpResponse = new HttpResponseMessage(statusCode)
    {
      RequestMessage = requestMessage,
      Content = new StringContent(responseContent),
    };
    var error = ApiException.Create(requestMessage, HttpMethod.Post, httpResponse, new RefitSettings(), null)
      .GetAwaiter()
      .GetResult();

    return new ApiResponse<OllamaGenerateResponse>(httpResponse, default, new RefitSettings(), error);
  }
}
