namespace MailAgent.Api.Configuration;

internal sealed class MailServerConfiguration
{
  public MailProvider? Provider { get; init; }
  public string? Username { get; init; }
  public string? Password { get; init; }
  public MailServerImapConfiguration Imap { get; init; } = new();
  public MailServerEwsConfiguration Ews { get; init; } = new();
}
