namespace MailAgent.Application.Import;

public sealed record MailSummary(
  string ExternalId,
  string MessageId,
  string Subject,
  string From,
  string Date,
  string? Body = null);
