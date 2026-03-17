namespace MailAgent.Application.Contracts.Mail.Models;

public sealed record StoredMail(
  int Id,
  string Folder,
  string MessageId,
  DateTimeOffset DateUtc,
  string From,
  string Subject,
  string RawBody,
  string MarkdownBody,
  string InsertedAt);
