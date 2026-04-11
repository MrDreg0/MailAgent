using MailAgent.Application.Llm;

namespace MailAgent.Api.Configuration;

internal sealed class LlmConfiguration
{
  public LlmProvider? Provider { get; init; }
  public string? BaseUrl { get; init; }
  public string? TimeoutMinutes { get; init; }
  public string? FastModel { get; init; }
  public string? MainModel { get; init; }
}
