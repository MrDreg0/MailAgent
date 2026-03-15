using MailAgent.Domain.Mail;

namespace MailAgent.Application;

public interface IMailClient
{
  Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessage>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessage>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default);
}
