using MailAgent.Application;
using MailAgent.Application.Contracts;
using MailAgent.Application.Contracts.Mail;
using Microsoft.Extensions.DependencyInjection;

namespace MailAgent.Mail;

public static class DependencyInjection
{
  extension(IServiceCollection services)
  {
    public IServiceCollection AddImapMailClient(Imap.Settings settings)
    {
      ArgumentNullException.ThrowIfNull(settings);
      services.AddSingleton<IMailClient>(_ => new Imap.MailClient(settings));
      return services;
    }

    public IServiceCollection AddEwsMailClient(Ews.Settings settings)
    {
      ArgumentNullException.ThrowIfNull(settings);
      services.AddSingleton<IMailClient>(_ => new Ews.MailClient(settings));
      return services;
    }
  }
}
