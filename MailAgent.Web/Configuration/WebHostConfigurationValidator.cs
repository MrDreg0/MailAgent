using FluentValidation;

namespace MailAgent.Web.Configuration;

internal sealed class WebHostConfigurationValidator : AbstractValidator<WebHostConfiguration>
{
  public WebHostConfigurationValidator()
  {
    RuleFor(configuration => configuration.UseHttpsRedirection)
      .Cascade(CascadeMode.Stop)
      .NotEmpty()
      .WithName(nameof(WebHostConfiguration.UseHttpsRedirection))
      .WithMessage("{PropertyName} configuration is missing.")
      .Must(BeBoolean)
      .WithMessage("{PropertyName} must be a valid boolean.");
  }

  private static bool BeBoolean(string? value)
  {
    return bool.TryParse(value, out _);
  }
}
