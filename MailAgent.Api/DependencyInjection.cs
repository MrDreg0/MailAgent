using MailAgent.Mail;
namespace MailAgent.Api;

internal static class DependencyInjection
{
  internal static IServiceCollection AddConfiguredMailClient(this IServiceCollection services, IConfiguration configuration)
  {
    var mailServerSection = configuration.GetSection("MailServer");
    var provider = mailServerSection["Provider"]?.Trim().ToLowerInvariant();
    var username = mailServerSection["Username"]
      ?? throw new InvalidOperationException("MailServer:Username configuration is missing");
    var password = mailServerSection["Password"]
      ?? throw new InvalidOperationException("MailServer:Password configuration is missing");

    return provider switch
    {
      "imap" => services.AddImapMailClient(Settings.CreateImapSettings(mailServerSection, username, password)),
      "ews" => services.AddEwsMailClient(Settings.CreateEwsSettings(mailServerSection, username, password)),
      _ => throw new InvalidOperationException($"Unsupported mail provider '{provider}'. Use 'ews' or 'imap'.")
    };
  }
}
