namespace MailAgent.Application.Digest;

public sealed record DailyDigestBuildResult(
  string Folder,
  DateOnly DigestDate,
  int TotalFetched,
  int Selected,
  string DigestMarkdown);
