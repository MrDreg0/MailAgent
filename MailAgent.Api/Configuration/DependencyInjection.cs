using FluentValidation;
using Microsoft.Extensions.Options;

namespace MailAgent.Api.Configuration;

internal static class DependencyInjection
{
  internal static IServiceCollection AddValidatedConfiguration(this IServiceCollection services, IConfiguration configuration)
  {
    AddValidatedOptions<ConnectionStringsConfiguration, ConnectionStringsConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("ConnectionStrings")));

    AddValidatedOptions<LlmConfiguration, LlmConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("Llm")));

    AddValidatedOptions<MailImportConfiguration, MailImportConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("MailImport")));

    AddValidatedOptions<DailyDigestConfiguration, DailyDigestConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("DailyDigest")));

    AddValidatedOptions<MailServerConfiguration, MailServerConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("MailServer")));

    services.AddSingleton(sp => sp.GetRequiredService<IOptions<LlmConfiguration>>().Value.ToRuntimeSettings());
    services.AddSingleton(sp => sp.GetRequiredService<IOptions<MailImportConfiguration>>().Value.ToRuntimeSettings());
    services.AddSingleton(sp => sp.GetRequiredService<IOptions<DailyDigestConfiguration>>().Value.ToRuntimeSettings());

    return services;
  }

  private static void AddValidatedOptions<TOptions, TValidator>(
    IServiceCollection services,
    Action<OptionsBuilder<TOptions>> configureOptions)
    where TOptions : class
    where TValidator : class, IValidator<TOptions>
  {
    services.AddSingleton<IValidator<TOptions>, TValidator>();
    services.AddSingleton<IValidateOptions<TOptions>>(serviceProvider =>
      new FluentValidateOptions<TOptions>(
        Options.DefaultName,
        serviceProvider.GetRequiredService<IValidator<TOptions>>()));

    configureOptions(
      services.AddOptions<TOptions>()
        .ValidateOnStart());
  }
}
