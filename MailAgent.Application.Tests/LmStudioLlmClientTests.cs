using System.Net;
using System.Text.Json;
using MailAgent.Application.Llm;
using MailAgent.Application.LmStudio;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace MailAgent.Application.Tests;

[TestFixture]
public class LmStudioLlmClientTests
{
  [Test]
  public async Task Generate_SendsPromptAsUserChatMessageAndReturnsFirstChoiceContent()
  {
    // Given.
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    var api = new FakeLmStudioApi(CreateSuccessResponse(new LmStudioChatCompletionResponse([
      new LmStudioChatCompletionChoice(new LmStudioChatMessage("assistant", " result text ")),
    ])));
    var sut = new LmStudioLlmClient(api, NullLogger<LmStudioLlmClient>.Instance);

    // When.
    var result = await sut.Generate(new LlmGenerateRequest("local-model", "prompt text"), cancellationToken);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Response, Is.EqualTo(" result text "));
      Assert.That(api.Request, Is.Not.Null);
      Assert.That(api.Request!.Model, Is.EqualTo("local-model"));
      Assert.That(api.Request.Stream, Is.False);
      Assert.That(api.Request.Ttl, Is.EqualTo(5));
      Assert.That(api.Request.Messages, Has.Count.EqualTo(1));
      Assert.That(api.Request.Messages[0].Role, Is.EqualTo("user"));
      Assert.That(api.Request.Messages[0].Content, Is.EqualTo("prompt text"));
      Assert.That(api.CancellationToken, Is.EqualTo(cancellationToken));
    });
  }

  [Test]
  public void Serialize_RequestUsesOpenAiCompatiblePropertyNames()
  {
    // Given.
    var request = new LmStudioChatCompletionRequest(
      Model: "local-model",
      Messages: [new LmStudioChatMessage("user", "prompt text")],
      Stream: false,
      Ttl: 5);

    // When.
    var json = JsonSerializer.Serialize(request);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(json, Does.Contain("\"model\""));
      Assert.That(json, Does.Contain("\"messages\""));
      Assert.That(json, Does.Contain("\"role\""));
      Assert.That(json, Does.Contain("\"content\""));
      Assert.That(json, Does.Contain("\"stream\""));
      Assert.That(json, Does.Contain("\"ttl\""));
    });
  }

  [Test]
  public void Generate_ThrowsApiException_WhenLmStudioReturnsUnsuccessfulResponse()
  {
    // Given.
    var api = new FakeLmStudioApi(CreateFailureResponse(HttpStatusCode.BadRequest, "bad request"));
    var sut = new LmStudioLlmClient(api, NullLogger<LmStudioLlmClient>.Instance);

    // When.
    var act = async () => await sut.Generate(new LlmGenerateRequest("local-model", "prompt text"), CancellationToken.None);

    // Then.
    Assert.That(act, Throws.TypeOf<ApiException>());
  }

  private sealed class FakeLmStudioApi(ApiResponse<LmStudioChatCompletionResponse> response) : ILmStudioApi
  {
    public LmStudioChatCompletionRequest? Request { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    public Task<ApiResponse<LmStudioChatCompletionResponse>> CreateChatCompletion(
      LmStudioChatCompletionRequest request,
      CancellationToken cancellationToken)
    {
      Request = request;
      CancellationToken = cancellationToken;

      return Task.FromResult(response);
    }
  }

  private static ApiResponse<LmStudioChatCompletionResponse> CreateSuccessResponse(LmStudioChatCompletionResponse content)
  {
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost/v1/chat/completions"),
    };

    return new ApiResponse<LmStudioChatCompletionResponse>(httpResponse, content, new RefitSettings(), null);
  }

  private static ApiResponse<LmStudioChatCompletionResponse> CreateFailureResponse(HttpStatusCode statusCode, string responseContent)
  {
    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost/v1/chat/completions");
    var httpResponse = new HttpResponseMessage(statusCode)
    {
      RequestMessage = requestMessage,
      Content = new StringContent(responseContent),
    };
    var error = ApiException.Create(requestMessage, HttpMethod.Post, httpResponse, new RefitSettings(), null)
      .GetAwaiter()
      .GetResult();

    return new ApiResponse<LmStudioChatCompletionResponse>(httpResponse, default, new RefitSettings(), error);
  }
}
