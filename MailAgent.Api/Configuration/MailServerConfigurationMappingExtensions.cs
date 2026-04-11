using System.Globalization;
using MailKit.Security;

namespace MailAgent.Api.Configuration;

internal static class MailServerConfigurationMappingExtensions
{
  extension(MailServerConfiguration configuration)
  {
    internal MailProvider GetProvider()
    {
      return configuration.Provider!.Value;
    }

    internal Mail.Imap.Settings ToImapSettings()
    {
      return new Mail.Imap.Settings
      {
        Username = configuration.Username!.Trim(),
        Password = configuration.Password!,
        Host = configuration.Imap.Host!.Trim(),
        Port = int.Parse(configuration.Imap.Port!, CultureInfo.InvariantCulture),
        Security = Enum.Parse<SecureSocketOptions>(configuration.Imap.Security!, ignoreCase: true),
      };
    }

    internal Mail.Ews.Settings ToEwsSettings()
    {
      return new Mail.Ews.Settings
      {
        Username = configuration.Username!.Trim(),
        Password = configuration.Password!,
        Url = configuration.Ews.Url!.Trim(),
        Domain = string.IsNullOrWhiteSpace(configuration.Ews.Domain)
          ? null
          : configuration.Ews.Domain.Trim(),
      };
    }
  }
}
