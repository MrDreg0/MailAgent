using FluentValidation;

namespace MailAgent.Api.Configuration;

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
