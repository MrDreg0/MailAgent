using System.Globalization;
using MailKit.Security;

namespace MailAgent.Api.Configuration;

internal static class ConfigurationValuePredicates
{
  internal static bool IsBoolean(string? value)
  {
    return bool.TryParse(value, out _);
  }

  internal static bool IsInteger(string? value)
  {
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
  }

  internal static bool IsPositiveInteger(string? value)
  {
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) && parsedValue > 0;
  }

  internal static bool IsTimeSpan(string? value)
  {
    return TimeSpan.TryParse(value, out _);
  }

  internal static bool IsTimeOnly(string? value)
  {
    return TimeOnly.TryParse(value, out _);
  }

  internal static bool IsPositiveTimeSpan(string? value)
  {
    return TimeSpan.TryParse(value, out var parsedValue) && parsedValue > TimeSpan.Zero;
  }

  internal static bool IsNonNegativeTimeSpan(string? value)
  {
    return TimeSpan.TryParse(value, out var parsedValue) && parsedValue >= TimeSpan.Zero;
  }

  internal static bool IsAbsoluteUri(string? value)
  {
    return Uri.TryCreate(value, UriKind.Absolute, out _);
  }

  internal static bool IsSecureSocketOption(string? value)
  {
    return Enum.TryParse(value, ignoreCase: true, out SecureSocketOptions _);
  }
}
