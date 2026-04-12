namespace MailAgent.Api.BackgroundServices;

internal sealed record DailyDigestBackgroundSettings(
  bool Enabled,
  bool? RunOnStartup,
  TimeSpan? Interval,
  string? Folder,
  TimeSpan? InitialBackfillPeriod,
  TimeOnly? GenerateAfter);
