using MailAgent.Application.Contracts.Mail.Models;
namespace MailAgent.Application.Contracts.Mail;

public interface IMailRepository
{
  Task SaveNewAsync(IReadOnlyCollection<StoredMail> mails, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StoredMail>> GetLatestFromFolderAsync(string folderName, int takeCount, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StoredMail>> GetByPeriodFromFolder(string folderName, TimeSpan period, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StoredMail>> GetByUtcRangeFromFolder(
    string folderName,
    DateTimeOffset fromUtc,
    DateTimeOffset toUtc,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StoredMail>> GetPage(int skip, int take, CancellationToken cancellationToken = default);

  Task<int> GetCount(CancellationToken cancellationToken = default);

  Task<IReadOnlySet<string>> GetExistingMessageIds(IReadOnlyCollection<string> messageIds, CancellationToken cancellationToken = default);

  Task<DateTimeOffset?> GetLatestDateUtcByFolder(string folderName, CancellationToken cancellationToken = default);
}
