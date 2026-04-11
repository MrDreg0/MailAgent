using FluentValidation;
using Microsoft.Extensions.Options;

namespace MailAgent.Web.Configuration;

internal static class DependencyInjection
{
  internal static IServiceCollection AddValidatedConfiguration(this IServiceCollection services, IConfiguration configuration)
  {
    AddValidatedOptions<ConnectionStringsConfiguration, ConnectionStringsConfigurationValidator>(
      services,
      options => options.Bind(configuration.GetSection("ConnectionStrings")));

    AddValidatedOptions<WebHostConfiguration, WebHostConfigurationValidator>(
      services,
      options => options.Bind(configuration));

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
