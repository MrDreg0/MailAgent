namespace MailAgent.Application.Llm;

public sealed class LlmSettings
{
  public required string Provider { get; init; }
  public required string BaseUrl { get; init; }
  public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
  public required string FastModel { get; init; }
  public required string MainModel { get; init; }
}
