namespace MailAgent.Mail;

public interface IMailClient
{
  Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessageDto>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<MailMessageDto>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default);
}
