using FluentValidation;
using Microsoft.Extensions.Options;

namespace MailAgent.Web.Configuration;

internal sealed class FluentValidateOptions<TOptions>(
  string? optionsName,
  IValidator<TOptions> validator) : IValidateOptions<TOptions>
  where TOptions : class
{
  public ValidateOptionsResult Validate(string? name, TOptions options)
  {
    if (optionsName is not null && optionsName != name)
    {
      return ValidateOptionsResult.Skip;
    }

    var validationResult = validator.Validate(options);

    if (validationResult.IsValid)
    {
      return ValidateOptionsResult.Success;
    }

    return ValidateOptionsResult.Fail(validationResult.Errors.Select(error => error.ErrorMessage));
  }
}
