using MailAgent.Application;
using MailAgent.Domain.Mail;
using MailAgent.Settings;
using MailKit;
using MailKit.Net.Imap;
using MessageSummaryItems = MailKit.MessageSummaryItems;

namespace MailAgent.Mail;

public sealed class ImapMailClient(ImapSettings imapSettings) : IMailClient
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

  private async Task<ImapClient> ConnectAsync(CancellationToken cancellationToken)
  {
    var client = new ImapClient();

    await client.ConnectAsync(imapSettings.Host, imapSettings.Port, imapSettings.Security, cancellationToken);
    await client.AuthenticateAsync(imapSettings.Username, imapSettings.Password, cancellationToken);

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
