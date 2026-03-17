namespace MailAgent.Application.Import;

public sealed record MailImportResult(IReadOnlyList<MailSummary> Latest)
{
  public int Total => Latest.Count;
}
