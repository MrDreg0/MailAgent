using MailAgent.Api.BackgroundServices;

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
        GenerateAfter: null);
    }

    return new DailyDigestBackgroundSettings(
      Enabled: true,
      RunOnStartup: bool.Parse(configuration.RunOnStartup!),
      Interval: TimeSpan.Parse(configuration.Interval!),
      Folder: configuration.Folder!.Trim(),
      GenerateAfter: TimeOnly.Parse(configuration.GenerateAfter!));
  }
}
