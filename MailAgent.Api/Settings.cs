using MailAgent.Application;
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
}
