namespace MailAgent.Application;

public interface IMailRepository
{
  Task SaveNewAsync(IReadOnlyCollection<StoredMail> mails, CancellationToken cancellationToken = default);
}
