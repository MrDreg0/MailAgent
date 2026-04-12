using MailAgent.Application.Contracts.Digest;

namespace MailAgent.Web.Browse;

public sealed class DailyDigestBrowserService(IDailyDigestRepository dailyDigestRepository)
{
  public async Task<DailyDigestPageResult> GetPage(
    string folderName,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    if (pageNumber <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be positive.");
    }

    if (pageSize <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be positive.");
    }

    var skip = (pageNumber - 1) * pageSize;
    var totalCount = await dailyDigestRepository.GetCount(folderName, cancellationToken);
    var items = await dailyDigestRepository.GetPage(folderName, skip, pageSize, cancellationToken);

    return new DailyDigestPageResult(
      Items: items,
      TotalCount: totalCount,
      PageNumber: pageNumber,
      PageSize: pageSize);
  }
}
