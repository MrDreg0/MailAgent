using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("MailAgent.Application.Tests")]
namespace MailAgent.Application;

public sealed class MailImportService(
  EmailBodyConverter bodyConverter,
  IMailClient mailClient,
  IMailRepository mailRepository)
{
  public async Task<MailImportResult> ImportLatestFromFolderAsync(
    string folderName,
    int takeCount,
    CancellationToken cancellationToken = default)
  {
    var fetchedMessages = await mailClient.GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
    var messageSummaries = new List<MailSummary>(capacity: fetchedMessages.Count);
    var mailCandidates = new List<StoredMail>(capacity: fetchedMessages.Count);

    foreach (var message in fetchedMessages)
    {
      var markdownBody = bodyConverter.ConvertToMarkdown(message.HtmlBody, message.TextBody);
      var candidate = MapToStoredMail(folderName, message, markdownBody, DateTimeOffset.UtcNow);

      if (candidate is not null)
      {
        mailCandidates.Add(candidate);
      }

      messageSummaries.Add(new MailSummary(
        message.ExternalId,
        message.NormalizedMessageId,
        message.Subject,
        message.From,
        message.DateUtc.ToString("u"),
        markdownBody));
    }

    await mailRepository.SaveNewAsync(mailCandidates, cancellationToken);

    return new MailImportResult(messageSummaries);
  }

  internal static StoredMail? MapToStoredMail(string folderName, MailMessage message, string markdownBody, DateTimeOffset insertedAtUtc)
  {
    if (string.IsNullOrWhiteSpace(message.NormalizedMessageId))
    {
      return null;
    }

    return new(
      Id: 0,
      Folder: folderName,
      MessageId: message.NormalizedMessageId,
      DateUtc: message.DateUtc.ToUniversalTime(),
      From: message.From,
      Subject: message.Subject,
      RawBody: message.RawBody,
      MarkdownBody: markdownBody,
      InsertedAt: insertedAtUtc.ToString("u"));
  }
}

public sealed record MailImportResult(IReadOnlyList<MailSummary> Latest)
{
  public int Total => Latest.Count;
}

public sealed record MailSummary(
  string ExternalId,
  string MessageId,
  string Subject,
  string From,
  string Date,
  string? Body = null);
