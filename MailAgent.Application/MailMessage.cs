namespace MailAgent.Application;

public sealed record MailMessage(
  string ExternalId,
  string MessageId,
  string Subject,
  string From,
  DateTimeOffset DateUtc,
  string? HtmlBody,
  string? TextBody)
{
  public string NormalizedMessageId => MessageId.Trim();

  public string RawBody => HtmlBody ?? TextBody ?? string.Empty;
}
