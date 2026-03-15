using MailKit.Security;
namespace MailAgent.Mail.Imap;

public class Settings : MailServerSettings
{
  public required string Host { get; set; }
  public int Port { get; set; }
  public SecureSocketOptions Security { get; set; }
}
