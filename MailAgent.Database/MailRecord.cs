namespace MailAgent.Database;

public sealed record MailRecord(
  int Id,
  string Folder,
  string MessageId,
  DateTimeOffset DateUtc,
  string From,
  string Subject,
  string RawBody,
  string MarkdownBody,
  string InsertedAt);
