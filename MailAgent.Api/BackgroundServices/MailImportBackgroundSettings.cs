namespace MailAgent.Api.BackgroundServices;

internal sealed record MailImportBackgroundSettings(
  bool Enabled,
  bool? RunOnStartup,
  TimeSpan? Interval,
  TimeSpan? InitialLookbackPeriod,
  TimeSpan? OverlapPeriod,
  IReadOnlyList<string> Folders);
