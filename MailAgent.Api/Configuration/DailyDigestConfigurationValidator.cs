using FluentValidation;

namespace MailAgent.Api.Configuration;

internal sealed class DailyDigestConfigurationValidator : AbstractValidator<DailyDigestConfiguration>
{
  public DailyDigestConfigurationValidator()
  {
    RuleFor(configuration => configuration.Enabled)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(DailyDigestConfiguration.Enabled))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(ConfigurationValuePredicates.IsBoolean)
      .WithMessage("{PropertyName} must be a valid boolean.");

    When(IsEnabled, () =>
    {
      RuleFor(configuration => configuration.RunOnStartup)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(DailyDigestConfiguration.RunOnStartup))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsBoolean)
        .WithMessage("{PropertyName} must be a valid boolean.");

      RuleFor(configuration => configuration.Interval)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(DailyDigestConfiguration.Interval))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsTimeSpan)
        .WithMessage("{PropertyName} must be a valid TimeSpan.")
        .Must(ConfigurationValuePredicates.IsPositiveTimeSpan)
        .WithMessage("{PropertyName} must be greater than zero.");

      RuleFor(configuration => configuration.Folder)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(DailyDigestConfiguration.Folder))
        .WithMessage("{PropertyName} configuration is missing.");

      RuleFor(configuration => configuration.GenerateAfter)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(DailyDigestConfiguration.GenerateAfter))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsTimeOnly)
        .WithMessage("{PropertyName} must be a valid TimeOnly.");
    });
  }

  private static bool IsEnabled(DailyDigestConfiguration configuration)
  {
    return bool.TryParse(configuration.Enabled, out var enabled) && enabled;
  }
}
