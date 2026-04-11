namespace MailAgent.Application.Llm;

public sealed class LlmSettings
{
  public required LlmProvider Provider { get; init; }
  public required string BaseUrl { get; init; }
  public required TimeSpan Timeout { get; init; }
  public required string FastModel { get; init; }
  public required string MainModel { get; init; }
}
