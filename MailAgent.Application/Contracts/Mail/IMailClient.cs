using MailAgent.Application.Contracts.Mail.Models;
namespace MailAgent.Application.Contracts.Mail;

public interface IMailClient
{
  Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessage>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessage>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessageIdentifier>> GetMessageIdentifiersFromFolder(string folderPath, TimeSpan period, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessage>> GetMessagesByExternalIds(string folderPath, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default);
}
