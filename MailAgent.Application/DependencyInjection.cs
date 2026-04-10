using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
using MailAgent.Application.Llm;
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
    
    if (llmSettings.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
    {
      services.AddRefitClient<IOllamaApi>()
        .ConfigureHttpClient(client =>
        {
          client.BaseAddress = new Uri(llmSettings.BaseUrl);
          client.Timeout = llmSettings.Timeout;
        });

      services.AddScoped<ILlmClient, OllamaLlmClient>();
    }
    else
    {
      throw new InvalidOperationException($"Unsupported LLM provider '{llmSettings.Provider}'. Use 'ollama'.");
    }
    
    return services;
  }
}
