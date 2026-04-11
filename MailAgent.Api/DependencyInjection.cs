using MailAgent.Api.BackgroundServices;
using MailAgent.Api.Configuration;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Exceptions;
using Microsoft.Extensions.Options;
namespace MailAgent.Api;

internal static class DependencyInjection
{
  extension(IServiceCollection services)
  {
    internal IServiceCollection AddConfiguredMailClient()
    {
      services.AddSingleton<IMailClient>(serviceProvider =>
      {
        var configuration = serviceProvider.GetRequiredService<IOptions<MailServerConfiguration>>().Value;

        return configuration.GetProvider() switch
        {
          MailProvider.Imap => new Mail.Imap.MailClient(configuration.ToImapSettings()),
          MailProvider.Ews => new Mail.Ews.MailClient(configuration.ToEwsSettings()),
          _ => throw new UnsupportedMailProviderException(configuration.Provider?.ToString() ?? "<null>")
        };
      });

      return services;
    }

    internal IServiceCollection AddMailImportBackgroundService()
    {
      services.AddHostedService<MailImportBackgroundService>();

      return services;
    }
  }
}
