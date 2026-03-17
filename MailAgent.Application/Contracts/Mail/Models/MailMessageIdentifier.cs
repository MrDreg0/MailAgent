namespace MailAgent.Application.Contracts.Mail.Models;

public sealed record MailMessageIdentifier(
  string ExternalId,
  string MessageId,
  string Subject,
  string From,
  DateTimeOffset DateUtc)
{
  public string NormalizedMessageId => MessageId.Trim();
}
