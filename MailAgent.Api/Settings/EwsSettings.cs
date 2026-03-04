namespace MailAgent.Settings;

public class EwsSettings : MailServerSettings
{
  public string? Url { get; set; }
  public string? Domain { get; set; }
}
