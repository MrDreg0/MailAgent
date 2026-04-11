namespace MailAgent.Api.Configuration;

internal sealed class MailImportConfiguration
{
  public string? Enabled { get; init; }
  public string? RunOnStartup { get; init; }
  public string? Interval { get; init; }
  public string? InitialLookbackPeriod { get; init; }
  public string? OverlapPeriod { get; init; }
  public string[]? Folders { get; init; }
}
