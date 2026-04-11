using FluentValidation;

namespace MailAgent.Api.Configuration;

internal sealed class MailImportConfigurationValidator : AbstractValidator<MailImportConfiguration>
{
  public MailImportConfigurationValidator()
  {
    RuleFor(configuration => configuration.Enabled)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(MailImportConfiguration.Enabled))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(ConfigurationValuePredicates.IsBoolean)
      .WithMessage("{PropertyName} must be a valid boolean.");

    RuleFor(configuration => configuration.Folders)
      .Must(NotContainEmptyValues)
      .WithName(nameof(MailImportConfiguration.Folders))
      .WithMessage("{PropertyName} must not contain empty values.")
      .When(configuration => configuration.Folders is not null);

    When(IsEnabled, () =>
    {
      RuleFor(configuration => configuration.RunOnStartup)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(MailImportConfiguration.RunOnStartup))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsBoolean)
        .WithMessage("{PropertyName} must be a valid boolean.");

      RuleFor(configuration => configuration.Interval)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(MailImportConfiguration.Interval))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsTimeSpan)
        .WithMessage("{PropertyName} must be a valid TimeSpan.")
        .Must(ConfigurationValuePredicates.IsPositiveTimeSpan)
        .WithMessage("{PropertyName} must be greater than zero.");

      RuleFor(configuration => configuration.InitialLookbackPeriod)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(MailImportConfiguration.InitialLookbackPeriod))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsTimeSpan)
        .WithMessage("{PropertyName} must be a valid TimeSpan.")
        .Must(ConfigurationValuePredicates.IsPositiveTimeSpan)
        .WithMessage("{PropertyName} must be greater than zero.");

      RuleFor(configuration => configuration.OverlapPeriod)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName(nameof(MailImportConfiguration.OverlapPeriod))
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsTimeSpan)
        .WithMessage("{PropertyName} must be a valid TimeSpan.")
        .Must(ConfigurationValuePredicates.IsNonNegativeTimeSpan)
        .WithMessage("{PropertyName} must be zero or greater.");

      RuleFor(configuration => configuration.Folders)
        .Cascade(CascadeMode.Stop)
        .NotNull()
        .WithName(nameof(MailImportConfiguration.Folders))
        .WithMessage("{PropertyName} must contain at least one folder when Enabled is true.")
        .Must(HaveAtLeastOneFolder)
        .WithMessage("{PropertyName} must contain at least one folder when Enabled is true.");
    });
  }

  private static bool IsEnabled(MailImportConfiguration configuration)
  {
    return bool.TryParse(configuration.Enabled, out var enabled) && enabled;
  }

  private static bool NotContainEmptyValues(string[]? folders)
  {
    return folders?.Any(string.IsNullOrWhiteSpace) != true;
  }

  private static bool HaveAtLeastOneFolder(string[]? folders)
  {
    return folders?.Any(folder => !string.IsNullOrWhiteSpace(folder)) == true;
  }
}
