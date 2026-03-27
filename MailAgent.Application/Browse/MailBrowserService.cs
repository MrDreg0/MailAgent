using MailAgent.Application.Contracts.Mail;

namespace MailAgent.Application.Browse;

public sealed class MailBrowserService(IMailRepository mailRepository)
{
  public async Task<MailPageResult> GetPage(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
  {
    if (pageNumber <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be positive.");
    }

    if (pageSize <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be positive.");
    }

    var skip = (pageNumber - 1) * pageSize;
    var totalCount = await mailRepository.GetCount(cancellationToken);
    var items = await mailRepository.GetPage(skip, pageSize, cancellationToken);

    return new MailPageResult(
      Items: items,
      TotalCount: totalCount,
      PageNumber: pageNumber,
      PageSize: pageSize);
  }
}
