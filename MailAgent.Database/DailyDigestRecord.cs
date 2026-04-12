namespace MailAgent.Database;

public sealed class DailyDigestRecord
{
  public int Id { get; set; }

  public string Folder { get; set; } = string.Empty;

  public DateOnly DigestDate { get; set; }

  public int TotalFetched { get; set; }

  public int Selected { get; set; }

  public string DigestMarkdown { get; set; } = string.Empty;

  public DateTimeOffset GeneratedAtUtc { get; set; }
}
