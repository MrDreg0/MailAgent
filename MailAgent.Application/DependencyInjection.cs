using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Digest;
using MailAgent.Application.Exceptions;
using MailAgent.Application.Import;
using MailAgent.Application.Llm;
using MailAgent.Application.LmStudio;
using MailAgent.Application.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace MailAgent.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddSingleton<EmailBodyConverter>()
      .AddScoped<MailImportService>()
      .AddScoped<ReleaseDigestService>()
      .AddScoped<OllamaLlmClient>()
      .AddScoped<LmStudioLlmClient>()
      .AddScoped<ILlmClient>(serviceProvider =>
      {
        var llmSettings = serviceProvider.GetRequiredService<LlmSettings>();

        return llmSettings.Provider switch
        {
          LlmProvider.Ollama => serviceProvider.GetRequiredService<OllamaLlmClient>(),
          LlmProvider.LmStudio => serviceProvider.GetRequiredService<LmStudioLlmClient>(),
          _ => throw new UnsupportedLlmProviderException(llmSettings.Provider.ToString())
        };
      });

    services.AddRefitClient<IOllamaApi>()
      .ConfigureHttpClient((serviceProvider, client) =>
      {
        var llmSettings = serviceProvider.GetRequiredService<LlmSettings>();
        client.BaseAddress = new Uri(llmSettings.BaseUrl);
        client.Timeout = llmSettings.Timeout;
      });

    services.AddRefitClient<ILmStudioApi>()
      .ConfigureHttpClient((serviceProvider, client) =>
      {
        var llmSettings = serviceProvider.GetRequiredService<LlmSettings>();
        client.BaseAddress = new Uri(llmSettings.BaseUrl);
        client.Timeout = llmSettings.Timeout;
      });

    return services;
  }
}
