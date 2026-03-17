using System.Runtime.CompilerServices;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
[assembly: InternalsVisibleTo("MailAgent.Application.Tests")]
namespace MailAgent.Application.Import;

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
    var (messageSummaries, mailCandidates) = BuildImportBatch(folderName, fetchedMessages);

    await mailRepository.SaveNewAsync(mailCandidates, cancellationToken);

    return new MailImportResult(
      Latest: messageSummaries,
      IdentifiersFound: fetchedMessages.Count,
      Loaded: fetchedMessages.Count,
      SaveCandidates: mailCandidates.Count);
  }

  public async Task<MailImportResult> ImportFromDate(
    string folderName,
    DateTimeOffset fromUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    var messageIdentifiers = await mailClient.GetMessageIdentifiersFromFolderSince(folderName, fromUtc, cancellationToken);
    var candidateIdentifiers = messageIdentifiers
      .Where(identifier => !string.IsNullOrWhiteSpace(identifier.NormalizedMessageId))
      .GroupBy(identifier => identifier.NormalizedMessageId, StringComparer.OrdinalIgnoreCase)
      .Select(group => group.First())
      .ToList();

    var existingMessageIds = await mailRepository.GetExistingMessageIds(
      candidateIdentifiers.Select(identifier => identifier.NormalizedMessageId).ToList(),
      cancellationToken);

    var identifiersToLoad = candidateIdentifiers
      .Where(identifier => !existingMessageIds.Contains(identifier.NormalizedMessageId))
      .ToList();

    if (identifiersToLoad.Count == 0)
    {
      return new MailImportResult(
        Latest: [],
        IdentifiersFound: messageIdentifiers.Count,
        AlreadyStored: existingMessageIds.Count,
        Loaded: 0,
        SaveCandidates: 0);
    }

    var fetchedMessages = await mailClient.GetMessagesByExternalIds(
      folderName,
      identifiersToLoad.Select(identifier => identifier.ExternalId).ToList(),
      cancellationToken);

    var (messageSummaries, mailCandidates) = BuildImportBatch(folderName, fetchedMessages);

    await mailRepository.SaveNewAsync(mailCandidates, cancellationToken);

    return new MailImportResult(
      Latest: messageSummaries,
      IdentifiersFound: messageIdentifiers.Count,
      AlreadyStored: existingMessageIds.Count,
      Loaded: fetchedMessages.Count,
      SaveCandidates: mailCandidates.Count);
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

  private (IReadOnlyList<MailSummary>, IReadOnlyCollection<StoredMail>) BuildImportBatch(
    string folderName,
    IReadOnlyList<MailMessage> fetchedMessages)
  {
    var insertedAtUtc = DateTimeOffset.UtcNow;
    var messageSummaries = new List<MailSummary>(capacity: fetchedMessages.Count);
    var mailCandidates = new List<StoredMail>(capacity: fetchedMessages.Count);

    foreach (var message in fetchedMessages)
    {
      var markdownBody = bodyConverter.ConvertToMarkdown(message.HtmlBody, message.TextBody);
      var candidate = MapToStoredMail(folderName, message, markdownBody, insertedAtUtc);

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

    return (messageSummaries, mailCandidates.OrderBy(mail => mail.DateUtc).ToList());
  }
}
