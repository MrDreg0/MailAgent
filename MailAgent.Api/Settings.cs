using MailAgent.Api.BackgroundServices;
using MailAgent.Application.Ollama;
using EwsSettings = MailAgent.Mail.Ews.Settings;
using ImapSettings = MailAgent.Mail.Imap.Settings;
using MailKit.Security;
namespace MailAgent.Api;

internal static class Settings
{
  internal static ImapSettings CreateImapSettings(IConfigurationSection mailServerSection, string username, string password)
  {
    var imapSection = mailServerSection.GetSection("Imap");
    if (!Enum.TryParse(imapSection["Security"], ignoreCase: true, out SecureSocketOptions secureSocketOptions))
    {
      throw new InvalidOperationException($"Invalid security setting '{imapSection["Security"]}' for IMAP provider.");
    }

    return new ImapSettings
    {
      Username = username,
      Password = password,
      Host = imapSection["Host"] ?? throw new InvalidOperationException("MailServer:Imap:Host configuration is missing"),
      Port = int.Parse(imapSection["Port"] ?? throw new InvalidOperationException("MailServer:Imap:Port configuration is missing")),
      Security = secureSocketOptions,
    };
  }

  internal static EwsSettings CreateEwsSettings(IConfigurationSection mailServerSection, string username, string password)
  {
    var ewsSection = mailServerSection.GetSection("Ews");

    return new EwsSettings
    {
      Username = username,
      Password = password,
      Url = ewsSection["Url"] ?? throw new InvalidOperationException("MailServer:Ews:Url configuration is missing"),
      Domain = ewsSection["Domain"],
    };
  }

  internal static OllamaSettings GetOllamaSettings(IConfiguration configuration)
  {
    var ollamaSection = configuration.GetSection("Ollama");
    var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434/";
    var timeoutMinutes = int.TryParse(ollamaSection["TimeoutMinutes"], out var parsedTimeoutMinutes)
      ? parsedTimeoutMinutes
      : 5;

    return new OllamaSettings
    {
      BaseUrl = baseUrl,
      Timeout = TimeSpan.FromMinutes(timeoutMinutes),
    };
  }

  internal static MailImportBackgroundSettings GetMailImportBackgroundSettings(IConfiguration configuration)
  {
    var section = configuration.GetSection("MailImport");
    var enabled = bool.TryParse(section["Enabled"], out var parsedEnabled) && parsedEnabled;
    var runOnStartup = !bool.TryParse(section["RunOnStartup"], out var parsedRunOnStartup) || parsedRunOnStartup;
    var interval = TryParseTimeSpan(section["Interval"], TimeSpan.FromHours(1), "MailImport:Interval");
    var lookbackPeriod = TryParseTimeSpan(section["LookbackPeriod"], interval, "MailImport:LookbackPeriod");
    var folders = section
      .GetSection("Folders")
      .GetChildren()
      .Select(child => child.Value)
      .Where(folder => !string.IsNullOrWhiteSpace(folder))
      .Select(folder => folder!)
      .ToList();

    if (interval <= TimeSpan.Zero)
    {
      throw new InvalidOperationException("MailImport:Interval must be greater than zero.");
    }

    if (lookbackPeriod <= TimeSpan.Zero)
    {
      throw new InvalidOperationException("MailImport:LookbackPeriod must be greater than zero.");
    }

    return new MailImportBackgroundSettings(
      Enabled: enabled,
      RunOnStartup: runOnStartup,
      Interval: interval,
      LookbackPeriod: lookbackPeriod,
      Folders: folders.Count == 0 ? ["/"] : folders);
  }

  private static TimeSpan TryParseTimeSpan(string? value, TimeSpan fallbackValue, string settingPath)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return fallbackValue;
    }

    if (!TimeSpan.TryParse(value, out var parsedValue))
    {
      throw new InvalidOperationException($"{settingPath} must be a valid TimeSpan.");
    }

    return parsedValue;
  }
}
