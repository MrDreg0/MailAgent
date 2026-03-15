using MailAgent.Application;
using Microsoft.EntityFrameworkCore;

namespace MailAgent.Database;

public sealed class MailRepository(DataContext dbContext) : IMailRepository
{
  public async Task SaveNewAsync(IReadOnlyCollection<StoredMail> mails, CancellationToken cancellationToken = default)
  {
    if (mails.Count == 0)
    {
      return;
    }

    var messageIds = mails
      .Select(mail => mail.MessageId)
      .Where(messageId => !string.IsNullOrWhiteSpace(messageId))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var existingMessageIds = messageIds.Count == 0
      ? []
      : await dbContext.Mails
        .Where(mail => messageIds.Contains(mail.MessageId))
        .Select(mail => mail.MessageId)
        .ToListAsync(cancellationToken);

    var existingMessageIdSet = existingMessageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

    var newRecords = mails
      .Where(mail => !existingMessageIdSet.Contains(mail.MessageId))
      .Select(MapToRecord)
      .ToList();

    if (newRecords.Count > 0)
    {
      await dbContext.Mails.AddRangeAsync(newRecords, cancellationToken);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private static MailRecord MapToRecord(StoredMail mail)
    => new(
      Id: mail.Id,
      Folder: mail.Folder,
      MessageId: mail.MessageId,
      DateUtc: mail.DateUtc,
      From: mail.From,
      Subject: mail.Subject,
      RawBody: mail.RawBody,
      MarkdownBody: mail.MarkdownBody,
      InsertedAt: mail.InsertedAt);
}
