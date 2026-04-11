using FluentValidation;

namespace MailAgent.Api.Configuration;

internal sealed class LlmConfigurationValidator : AbstractValidator<LlmConfiguration>
{
  public LlmConfigurationValidator()
  {
    RuleFor(configuration => configuration.Provider)
      .Cascade(CascadeMode.Stop)
      .NotNull()
      .WithName(nameof(LlmConfiguration.Provider))
      .WithMessage("{PropertyName} configuration is missing.")
      .IsInEnum()
      .WithMessage("{PropertyName} has an invalid value.");

    RuleFor(configuration => configuration.BaseUrl)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(LlmConfiguration.BaseUrl))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(ConfigurationValuePredicates.IsAbsoluteUri)
      .WithMessage("{PropertyName} must be a valid absolute URI.");

    RuleFor(configuration => configuration.TimeoutMinutes)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(LlmConfiguration.TimeoutMinutes))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(ConfigurationValuePredicates.IsInteger)
      .WithMessage("{PropertyName} must be a valid integer.")
      .Must(ConfigurationValuePredicates.IsPositiveInteger)
      .WithMessage("{PropertyName} must be greater than zero.");

    RuleFor(configuration => configuration.FastModel)
      .NotEmpty()
      .WithName(nameof(LlmConfiguration.FastModel))
      .WithMessage("{PropertyName} configuration is missing.");

    RuleFor(configuration => configuration.MainModel)
      .NotEmpty()
      .WithName(nameof(LlmConfiguration.MainModel))
      .WithMessage("{PropertyName} configuration is missing.");
  }
}
