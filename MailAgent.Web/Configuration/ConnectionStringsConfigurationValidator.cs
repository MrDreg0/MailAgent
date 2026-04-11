using FluentValidation;

namespace MailAgent.Web.Configuration;

internal sealed class ConnectionStringsConfigurationValidator : AbstractValidator<ConnectionStringsConfiguration>
{
  public ConnectionStringsConfigurationValidator()
  {
    RuleFor(configuration => configuration.Database)
      .NotEmpty()
      .WithName(nameof(ConnectionStringsConfiguration.Database))
      .WithMessage("{PropertyName} configuration is missing.");
  }
}
