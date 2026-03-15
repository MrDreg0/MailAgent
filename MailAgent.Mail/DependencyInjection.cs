using MailAgent.Application;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MailAgent.Mail;

public static class DependencyInjection
{
  public static IServiceCollection AddMailClient(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddSingleton<IMailClient>(_ =>
    {
      var mailServerSettingsSection = configuration.GetSection("MailServer");
      var provider = mailServerSettingsSection["Provider"]?.Trim().ToLowerInvariant();

      return provider switch
      {
        "imap" => CreateImapMailClient(mailServerSettingsSection),
        "ews" => CreateEwsClient(mailServerSettingsSection),
        _ => throw new InvalidOperationException($"Unsupported mail provider '{provider}'. Use 'ews' or 'imap'.")
      };
    });

    return services;
  }

  private static Ews.MailClient CreateEwsClient(IConfigurationSection mailServerSettingsSection)
  {
    var ewsSection = mailServerSettingsSection.GetSection("Ews");
    var ewsSettings = new Ews.Settings
    {
      Username = mailServerSettingsSection["Username"] ?? throw new InvalidOperationException("MailServer:Username configuration is missing"),
      Password = mailServerSettingsSection["Password"] ?? throw new InvalidOperationException("MailServer:Password configuration is missing"),
      Domain = ewsSection["Domain"],
      Url = ewsSection["Url"],
    };

    return new Ews.MailClient(ewsSettings);
  }

  private static Imap.MailClient CreateImapMailClient(IConfigurationSection mailServerSettingsSection)
  {
    var imapSection = mailServerSettingsSection.GetSection("Imap");
    if (!Enum.TryParse(imapSection["Security"], ignoreCase: true, out SecureSocketOptions secureSocketOptions))
    {
      throw new InvalidOperationException($"Invalid security setting '{imapSection["Security"]}' for IMAP provider.");
    }

    var imapSettings = new Imap.Settings
    {
      Username = mailServerSettingsSection["Username"] ?? throw new InvalidOperationException("MailServer:Username configuration is missing"),
      Password = mailServerSettingsSection["Password"] ?? throw new InvalidOperationException("MailServer:Password configuration is missing"),
      Host = imapSection["Host"] ?? throw new InvalidOperationException("MailServer:Imap:Host configuration is missing"),
      Port = int.Parse(imapSection["Port"] ?? throw new InvalidOperationException("MailServer:Imap:Port configuration is missing")),
      Security = secureSocketOptions,
    };

    return new Imap.MailClient(imapSettings);
  }
}
