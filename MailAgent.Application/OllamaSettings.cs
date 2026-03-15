namespace MailAgent.Application;

public sealed class OllamaSettings
{
  public required string BaseUrl { get; init; }
  public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}
