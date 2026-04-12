using MailAgent.Api.BackgroundServices;
using MailAgent.Application.Digest;

namespace MailAgent.Api.Configuration;

internal static class DailyDigestConfigurationMappingExtensions
{
  internal static DailyDigestBackgroundSettings ToRuntimeSettings(this DailyDigestConfiguration configuration)
  {
    var enabled = bool.Parse(configuration.Enabled!);

    if (!enabled)
    {
      return new DailyDigestBackgroundSettings(
        Enabled: false,
        RunOnStartup: null,
        Interval: null,
        Folder: null,
        InitialBackfillPeriod: null,
        GenerateAfter: null);
    }

    return new DailyDigestBackgroundSettings(
      Enabled: true,
      RunOnStartup: bool.Parse(configuration.RunOnStartup!),
      Interval: TimeSpan.Parse(configuration.Interval!),
      Folder: configuration.Folder!.Trim(),
      InitialBackfillPeriod: TimeSpan.Parse(configuration.InitialBackfillPeriod!),
      GenerateAfter: TimeOnly.Parse(configuration.GenerateAfter!));
  }

  internal static DailyDigestSettings ToDigestSettings(this DailyDigestConfiguration configuration)
  {
    var outputLanguage = string.IsNullOrWhiteSpace(configuration.OutputLanguage)
      ? "English"
      : configuration.OutputLanguage.Trim();

    return new DailyDigestSettings(outputLanguage);
  }
}
