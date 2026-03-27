using MailAgent.Application.Contracts.Mail.Models;

namespace MailAgent.Application.Browse;

public sealed record MailPageResult(
  IReadOnlyList<StoredMail> Items,
  int TotalCount,
  int PageNumber,
  int PageSize);
