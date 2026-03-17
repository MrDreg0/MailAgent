using MailAgent.Application.Contracts;
using MailAgent.Application.Contracts.Ollama;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
using MailAgent.Application.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Refit;
namespace MailAgent.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services, OllamaSettings ollamaSettings)
  {
    services.AddSingleton<EmailBodyConverter>()
      .AddScoped<MailImportService>()
      .AddScoped<ReleaseDigestService>();
    
    services.AddRefitClient<IOllamaClient>()
      .ConfigureHttpClient(client =>
      {
        client.BaseAddress = new Uri(ollamaSettings.BaseUrl);
        client.Timeout = ollamaSettings.Timeout;
      });
    
    return services;
  }
}
