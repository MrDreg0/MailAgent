namespace MailAgent.Application.Import;

public sealed record MailImportResult(
  IReadOnlyList<MailSummary> Latest,
  int IdentifiersFound = 0,
  int AlreadyStored = 0,
  int Loaded = 0,
  int SaveCandidates = 0)
{
  public int Total => Latest.Count;
}
