using FluentValidation;

namespace MailAgent.Web.Configuration;

internal sealed class MailAgentApiConfigurationValidator : AbstractValidator<MailAgentApiConfiguration>
{
  public MailAgentApiConfigurationValidator()
  {
    RuleFor(configuration => configuration.BaseUrl)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(MailAgentApiConfiguration.BaseUrl))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(BeAbsoluteUri)
      .WithMessage("{PropertyName} must be a valid absolute URI.");

    RuleFor(configuration => configuration.TimeoutMinutes)
      .NotNull()
      .WithName(nameof(MailAgentApiConfiguration.TimeoutMinutes))
      .WithMessage("{PropertyName} configuration is missing.")
      .GreaterThan(0)
      .WithMessage("{PropertyName} must be greater than zero.");
  }

  private static bool BeAbsoluteUri(string? value)
  {
    return Uri.TryCreate(value, UriKind.Absolute, out _);
  }
}
