namespace MailAgent.Mail.Ews;

public class Settings : MailServerSettings
{
  public required string Url { get; set; }
  public string? Domain { get; set; }
}
