using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
using MailAgent.Application.Llm;
using MailAgent.Application.LmStudio;
using MailAgent.Application.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Refit;
namespace MailAgent.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(
    this IServiceCollection services,
    LlmSettings llmSettings)
  {
    services.AddSingleton<EmailBodyConverter>()
      .AddScoped<MailImportService>()
      .AddScoped<ReleaseDigestService>()
      .AddSingleton(llmSettings);
    
    switch (llmSettings.Provider.Trim().ToLowerInvariant())
    {
      case "ollama":
        services.AddRefitClient<IOllamaApi>()
          .ConfigureHttpClient(client =>
          {
            client.BaseAddress = new Uri(llmSettings.BaseUrl);
            client.Timeout = llmSettings.Timeout;
          });

        services.AddScoped<ILlmClient, OllamaLlmClient>();
        break;
      case "lmstudio":
        services.AddRefitClient<ILmStudioApi>()
          .ConfigureHttpClient(client =>
          {
            client.BaseAddress = new Uri(llmSettings.BaseUrl);
            client.Timeout = llmSettings.Timeout;
          });

        services.AddScoped<ILlmClient, LmStudioLlmClient>();
        break;
      default:
        throw new InvalidOperationException($"Unsupported LLM provider '{llmSettings.Provider}'. Use 'ollama' or 'lmstudio'.");
    }
    
    return services;
  }
}
