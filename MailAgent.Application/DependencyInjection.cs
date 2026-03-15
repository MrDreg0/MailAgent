using Microsoft.Extensions.DependencyInjection;
namespace MailAgent.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services, OllamaSettings ollamaSettings)
  {
    services.AddSingleton<EmailBodyConverter>()
      .AddScoped<MailImportService>()
      .AddScoped<ReleaseDigestService>()
      .AddSingleton<OllamaClient>();
    
    services.AddHttpClient("ollama", client =>
    {
      client.BaseAddress = new Uri(ollamaSettings.BaseUrl);
      client.Timeout = ollamaSettings.Timeout;
    });
    
    return services;
  }
}
