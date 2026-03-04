namespace MailAgent.Mail;

public sealed record MailMessageDto(
  string ExternalId,
  string MessageId,
  string Subject,
  string From,
  DateTimeOffset DateUtc,
  string? HtmlBody,
  string? TextBody);
