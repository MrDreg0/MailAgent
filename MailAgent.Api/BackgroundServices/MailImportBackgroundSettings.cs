namespace MailAgent.Api.BackgroundServices;

internal sealed record MailImportBackgroundSettings(
  bool Enabled,
  bool RunOnStartup,
  TimeSpan Interval,
  TimeSpan LookbackPeriod,
  IReadOnlyList<string> Folders);
