using FluentValidation;

namespace MailAgent.Api.Configuration;

internal sealed class MailServerConfigurationValidator : AbstractValidator<MailServerConfiguration>
{
  public MailServerConfigurationValidator()
  {
    RuleFor(configuration => configuration.Provider)
      .Cascade(CascadeMode.Stop)
      .NotNull()
      .WithName(nameof(MailServerConfiguration.Provider))
      .WithMessage("{PropertyName} configuration is missing.")
      .IsInEnum()
      .WithMessage("{PropertyName} has an invalid value.");

    RuleFor(configuration => configuration.Username)
      .NotEmpty()
      .WithName(nameof(MailServerConfiguration.Username))
      .WithMessage("{PropertyName} configuration is missing.");

    RuleFor(configuration => configuration.Password)
      .NotEmpty()
      .WithName(nameof(MailServerConfiguration.Password))
      .WithMessage("{PropertyName} configuration is missing.");

    When(IsImap, () =>
    {
      RuleFor(configuration => configuration.Imap.Host)
        .NotEmpty()
        .WithName($"{nameof(MailServerConfiguration.Imap)}.{nameof(MailServerImapConfiguration.Host)}")
        .WithMessage("{PropertyName} configuration is missing.");

      RuleFor(configuration => configuration.Imap.Port)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName($"{nameof(MailServerConfiguration.Imap)}.{nameof(MailServerImapConfiguration.Port)}")
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsInteger)
        .WithMessage("{PropertyName} must be a valid integer.")
        .Must(ConfigurationValuePredicates.IsPositiveInteger)
        .WithMessage("{PropertyName} must be greater than zero.");

      RuleFor(configuration => configuration.Imap.Security)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName($"{nameof(MailServerConfiguration.Imap)}.{nameof(MailServerImapConfiguration.Security)}")
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsSecureSocketOption)
        .WithMessage("Invalid {PropertyName} setting '{PropertyValue}' for IMAP provider.");
    });

    When(IsEws, () =>
    {
      RuleFor(configuration => configuration.Ews.Url)
        .Cascade(CascadeMode.Stop)
        .NotEmpty()
        .WithName($"{nameof(MailServerConfiguration.Ews)}.{nameof(MailServerEwsConfiguration.Url)}")
        .WithMessage("{PropertyName} configuration is missing.")
        .Must(ConfigurationValuePredicates.IsAbsoluteUri)
        .WithMessage("{PropertyName} must be a valid absolute URI.");
    });
  }

  private static bool IsImap(MailServerConfiguration configuration)
  {
    return configuration.Provider == MailProvider.Imap;
  }

  private static bool IsEws(MailServerConfiguration configuration)
  {
    return configuration.Provider == MailProvider.Ews;
  }

}
