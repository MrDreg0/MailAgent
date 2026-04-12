namespace MailAgent.Api.Configuration;

internal sealed class DailyDigestConfiguration
{
  public string? Enabled { get; init; }

  public string? RunOnStartup { get; init; }

  public string? Interval { get; init; }

  public string? Folder { get; init; }

  public string? GenerateAfter { get; init; }
}
