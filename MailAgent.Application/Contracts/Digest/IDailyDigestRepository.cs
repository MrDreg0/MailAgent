using MailAgent.Application.Contracts.Digest.Models;

namespace MailAgent.Application.Contracts.Digest;

public interface IDailyDigestRepository
{
  Task<int> GetCount(
    string folderName,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<DailyDigest>> GetPage(
    string folderName,
    int skip,
    int take,
    CancellationToken cancellationToken = default);

  Task<DailyDigest?> GetByDate(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<DailyDigest>> GetLatest(
    string folderName,
    int takeCount,
    CancellationToken cancellationToken = default);

  Task Save(DailyDigest digest, CancellationToken cancellationToken = default);
}
