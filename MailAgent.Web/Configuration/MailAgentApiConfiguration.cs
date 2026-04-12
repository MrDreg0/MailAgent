namespace MailAgent.Web.Configuration;

internal sealed class MailAgentApiConfiguration
{
  public string? BaseUrl { get; init; }
  public int? TimeoutMinutes { get; init; }
}
