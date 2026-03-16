namespace MailAgent.Application;

public interface IMailRepository
{
  Task SaveNewAsync(IReadOnlyCollection<StoredMail> mails, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StoredMail>> GetLatestFromFolderAsync(string folderName, int takeCount, CancellationToken cancellationToken = default);
  
  Task<IReadOnlyList<StoredMail>> GetByPeriodFromFolder(string folderName, TimeSpan period, CancellationToken cancellationToken = default);
}
