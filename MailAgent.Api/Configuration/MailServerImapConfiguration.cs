namespace MailAgent.Api.Configuration;

internal sealed class MailServerImapConfiguration
{
  public string? Host { get; init; }
  public string? Port { get; init; }
  public string? Security { get; init; }
}
