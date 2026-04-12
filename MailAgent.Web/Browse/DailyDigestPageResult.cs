using MailAgent.Application.Contracts.Digest.Models;

namespace MailAgent.Web.Browse;

public sealed record DailyDigestPageResult(
  IReadOnlyList<DailyDigest> Items,
  int TotalCount,
  int PageNumber,
  int PageSize);
