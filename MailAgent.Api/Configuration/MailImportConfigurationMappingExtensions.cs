using MailAgent.Api.BackgroundServices;

namespace MailAgent.Api.Configuration;

internal static class MailImportConfigurationMappingExtensions
{
  internal static MailImportBackgroundSettings ToRuntimeSettings(this MailImportConfiguration configuration)
  {
    var enabled = bool.Parse(configuration.Enabled!);

    if (!enabled)
    {
      return new MailImportBackgroundSettings(
        Enabled: false,
        RunOnStartup: null,
        Interval: null,
        InitialLookbackPeriod: null,
        OverlapPeriod: null,
        Folders: []);
    }

    return new MailImportBackgroundSettings(
      Enabled: true,
      RunOnStartup: bool.Parse(configuration.RunOnStartup!),
      Interval: TimeSpan.Parse(configuration.Interval!),
      InitialLookbackPeriod: TimeSpan.Parse(configuration.InitialLookbackPeriod!),
      OverlapPeriod: TimeSpan.Parse(configuration.OverlapPeriod!),
      Folders: configuration.Folders!
        .Where(folder => !string.IsNullOrWhiteSpace(folder))
        .Select(folder => folder!.Trim())
        .ToArray());
  }
}
