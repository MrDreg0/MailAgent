using MailKit.Security;
namespace MailAgent.Settings;

public class ImapSettings : MailServerSettings
{
  public string Host { get; set; }
  public int Port { get; set; }
  public SecureSocketOptions Security { get; set; }
}
