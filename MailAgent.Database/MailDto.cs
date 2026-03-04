namespace MailAgent.Database;

public record MailDto(
  int Id,
  string Folder,
  int ImapUid,
  string MessageId,
  DateTimeOffset DateUtc,
  string From,
  string Subject,
  string RawBody,
  string MarkdownBody,
  string InsertedAt);
