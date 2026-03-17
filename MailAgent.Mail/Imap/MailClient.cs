using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MessageSummaryItems = MailKit.MessageSummaryItems;

namespace MailAgent.Mail.Imap;

public sealed class MailClient(Settings settings) : IMailClient
{
  public async Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default)
  {
    using var imapClient = await ConnectAsync(cancellationToken);

    var folders = await imapClient.Inbox.GetSubfoldersAsync(cancellationToken: cancellationToken);

    return folders.Select(folder => folder.Name).ToList();
  }

  public async Task<IReadOnlyList<MailMessage>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    using var imapClient = await ConnectAsync(cancellationToken);
    var folder = await ResolveFolderAsync(imapClient.Inbox, folderPath, cancellationToken);

    return await GetLatestMessagesAsync(folder, takeCount, cancellationToken);
  }

  public async Task<IReadOnlyList<MailMessage>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default)
  {
    using var imapClient = await ConnectAsync(cancellationToken);

    return await GetLatestMessagesAsync(imapClient.Inbox, takeCount, cancellationToken);
  }

  public async Task<IReadOnlyList<MailMessageIdentifier>> GetMessageIdentifiersFromFolderSince(
    string folderPath,
    DateTimeOffset fromUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    using var imapClient = await ConnectAsync(cancellationToken);
    var folder = await ResolveFolderAsync(imapClient.Inbox, folderPath, cancellationToken);

    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

    var uniqueIds = await folder.SearchAsync(SearchQuery.DeliveredAfter(fromUtc.UtcDateTime.AddSeconds(-1)), cancellationToken);

    if (uniqueIds.Count == 0)
    {
      return [];
    }

    var summaries = await folder.FetchAsync(
      uniqueIds,
      MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope,
      cancellationToken);

    return summaries
      .Select(summary => new MailMessageIdentifier(
        ExternalId: summary.UniqueId.Id.ToString(),
        MessageId: summary.Envelope?.MessageId ?? string.Empty,
        Subject: summary.Envelope?.Subject ?? string.Empty,
        From: summary.Envelope?.From.ToString() ?? string.Empty,
        DateUtc: (summary.Envelope?.Date ?? DateTimeOffset.MinValue).ToUniversalTime()))
      .OrderByDescending(summary => summary.DateUtc)
      .ToList();
  }

  public async Task<IReadOnlyList<MailMessage>> GetMessagesByExternalIds(
    string folderPath,
    IReadOnlyCollection<string> externalIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    if (externalIds.Count == 0)
    {
      return [];
    }

    using var imapClient = await ConnectAsync(cancellationToken);
    var folder = await ResolveFolderAsync(imapClient.Inbox, folderPath, cancellationToken);

    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

    var uniqueIds = externalIds
      .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
      .Select(externalId => uint.TryParse(externalId, out var parsedUid) ? new UniqueId(parsedUid) : UniqueId.Invalid)
      .Where(uniqueId => uniqueId.IsValid)
      .Distinct()
      .ToList();

    if (uniqueIds.Count == 0)
    {
      return [];
    }

    var messages = new List<MailMessage>(uniqueIds.Count);

    foreach (var uniqueId in uniqueIds)
    {
      var mimeMessage = await folder.GetMessageAsync(uniqueId, cancellationToken);

      messages.Add(new MailMessage(
        ExternalId: uniqueId.Id.ToString(),
        MessageId: mimeMessage.MessageId ?? string.Empty,
        Subject: mimeMessage.Subject ?? string.Empty,
        From: mimeMessage.From.ToString(),
        DateUtc: mimeMessage.Date.ToUniversalTime(),
        HtmlBody: mimeMessage.HtmlBody,
        TextBody: mimeMessage.TextBody));
    }

    return messages;
  }

  private async Task<ImapClient> ConnectAsync(CancellationToken cancellationToken)
  {
    var client = new ImapClient();

    await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
    await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);

    return client;
  }

  private static async Task<IMailFolder> ResolveFolderAsync(IMailFolder root, string folderPath, CancellationToken cancellationToken)
  {
    var segments = folderPath.Split(['/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    var current = root;
    foreach (var segment in segments)
    {
      current = await current.GetSubfolderAsync(segment, cancellationToken);
    }

    return current;
  }

  private static async Task<IReadOnlyList<MailMessage>> GetLatestMessagesAsync(IMailFolder folder, int takeCount, CancellationToken cancellationToken)
  {
    if (takeCount <= 0)
    {
      return [];
    }

    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

    var totalCount = folder.Count;
    if (totalCount == 0)
    {
      return [];
    }

    var actualTakeCount = Math.Min(takeCount, totalCount);
    var startIndex = totalCount - actualTakeCount;

    var summaries = await folder.FetchAsync(
      startIndex,
      -1,
      new FetchRequest(MessageSummaryItems.UniqueId),
      cancellationToken);

    var messages = new List<MailMessage>(actualTakeCount);

    foreach (var summary in summaries.TakeLast(actualTakeCount))
    {
      var mimeMessage = await folder.GetMessageAsync(summary.UniqueId, cancellationToken);

      messages.Add(new MailMessage(
        ExternalId: summary.UniqueId.Id.ToString(),
        MessageId: mimeMessage.MessageId ?? string.Empty,
        Subject: mimeMessage.Subject ?? string.Empty,
        From: mimeMessage.From.ToString(),
        DateUtc: mimeMessage.Date.ToUniversalTime(),
        HtmlBody: mimeMessage.HtmlBody,
        TextBody: mimeMessage.TextBody));
    }

    return messages;
  }
}
