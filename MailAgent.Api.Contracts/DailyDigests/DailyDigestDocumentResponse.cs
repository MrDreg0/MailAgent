namespace MailAgent.Api.Contracts.DailyDigests;

public sealed record DailyDigestDocumentResponse(
  int Id,
  string Folder,
  DateOnly DigestDate,
  int TotalFetched,
  int Selected,
  string DigestMarkdown,
  DateTimeOffset GeneratedAtUtc);
