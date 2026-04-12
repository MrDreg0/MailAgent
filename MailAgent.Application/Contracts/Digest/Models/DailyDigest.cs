namespace MailAgent.Application.Contracts.Digest.Models;

public sealed record DailyDigest(
  int Id,
  string Folder,
  DateOnly DigestDate,
  int TotalFetched,
  int Selected,
  string DigestMarkdown,
  DateTimeOffset GeneratedAtUtc);
