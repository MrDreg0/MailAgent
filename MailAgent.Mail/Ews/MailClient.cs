using MailAgent.Application;
using MailAgent.Application.Contracts;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Import;
using Microsoft.Exchange.WebServices.Data;
using Task = System.Threading.Tasks.Task;

namespace MailAgent.Mail.Ews;

public sealed class MailClient(Settings settings) : IMailClient
{
  private const int PageSize = 200;

  public Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default)
  {
    var service = CreateService();

    var folderView = new FolderView(1000)
    {
      Traversal = FolderTraversal.Shallow
    };

    var folders = service.FindFolders(WellKnownFolderName.Inbox, folderView);

    IReadOnlyList<string> names = folders.Folders
      .Select(folder => folder.DisplayName)
      .ToList();

    return Task.FromResult(names);
  }

  public Task<IReadOnlyList<MailMessage>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    var service = CreateService();
    var folderId = ResolveFolderId(service, folderPath);

    return Task.FromResult(GetLatestMessages(service, folderId, takeCount));
  }

  public Task<IReadOnlyList<MailMessage>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default)
  {
    var service = CreateService();

    return Task.FromResult(GetLatestMessages(service, new FolderId(WellKnownFolderName.Inbox), takeCount));
  }

  public Task<IReadOnlyList<MailMessageIdentifier>> GetMessageIdentifiersFromFolder(
    string folderPath,
    TimeSpan period,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    if (period <= TimeSpan.Zero)
    {
      return Task.FromResult<IReadOnlyList<MailMessageIdentifier>>([]);
    }

    var service = CreateService();
    var folderId = ResolveFolderId(service, folderPath);
    var receivedAfterUtc = DateTime.UtcNow - period;
    var searchFilter = new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, receivedAfterUtc);
    var identifiers = new List<MailMessageIdentifier>();
    var offset = 0;

    while (true)
    {
      var itemView = new ItemView(PageSize, offset, OffsetBasePoint.Beginning)
      {
        PropertySet = new PropertySet(
          BasePropertySet.IdOnly,
          ItemSchema.Subject,
          ItemSchema.DateTimeReceived,
          EmailMessageSchema.InternetMessageId,
          EmailMessageSchema.From)
      };

      itemView.OrderBy.Add(ItemSchema.DateTimeReceived, SortDirection.Descending);

      var findResults = service.FindItems(folderId, searchFilter, itemView);

      identifiers.AddRange(findResults.Items
        .OfType<EmailMessage>()
        .Select(MapToIdentifier));

      if (!findResults.MoreAvailable)
      {
        break;
      }

      offset += findResults.Items.Count;
    }

    return Task.FromResult<IReadOnlyList<MailMessageIdentifier>>(identifiers);
  }

  public Task<IReadOnlyList<MailMessage>> GetMessagesByExternalIds(
    string folderPath,
    IReadOnlyCollection<string> externalIds,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    if (externalIds.Count == 0)
    {
      return Task.FromResult<IReadOnlyList<MailMessage>>([]);
    }

    var service = CreateService();
    var normalizedIds = externalIds
      .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
      .Distinct(StringComparer.Ordinal)
      .ToList();

    if (normalizedIds.Count == 0)
    {
      return Task.FromResult<IReadOnlyList<MailMessage>>([]);
    }

    // Resolve the folder up front so callers keep the same contract as other folder-based operations.
    _ = ResolveFolderId(service, folderPath);

    var propertySet = new PropertySet(
      BasePropertySet.FirstClassProperties,
      ItemSchema.Subject,
      ItemSchema.DateTimeReceived,
      EmailMessageSchema.InternetMessageId,
      EmailMessageSchema.From,
      ItemSchema.Body);

    var messages = normalizedIds
      .Select(externalId => EmailMessage.Bind(service, new ItemId(externalId), propertySet))
      .Select(MapToMailMessage)
      .ToList();

    return Task.FromResult<IReadOnlyList<MailMessage>>(messages);
  }

  private ExchangeService CreateService()
  {
    var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
    {
      Credentials = string.IsNullOrWhiteSpace(settings.Domain)
        ? new WebCredentials(settings.Username, settings.Password)
        : new WebCredentials(settings.Username, settings.Password, settings.Domain)
    };

    if (!string.IsNullOrWhiteSpace(settings.Url))
    {
      service.Url = new Uri(settings.Url);
      return service;
    }

    service.AutodiscoverUrl(settings.Username, redirectUrl =>
      Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri)
      && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    return service;
  }

  private static FolderId ResolveFolderId(ExchangeService service, string folderPath)
  {
    var currentFolderId = new FolderId(WellKnownFolderName.Inbox);
    var segments = folderPath.Split(['/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    foreach (var segment in segments)
    {
      var folderView = new FolderView(1000)
      {
        Traversal = FolderTraversal.Shallow
      };

      var childFolders = service.FindFolders(currentFolderId, folderView);
      var nextFolder = childFolders.Folders.FirstOrDefault(folder =>
        folder.DisplayName.Equals(segment, StringComparison.OrdinalIgnoreCase));

      if (nextFolder is null)
      {
        throw new InvalidOperationException($"Folder '{folderPath}' was not found under Inbox.");
      }

      currentFolderId = nextFolder.Id;
    }

    return currentFolderId;
  }

  private static IReadOnlyList<MailMessage> GetLatestMessages(ExchangeService service, FolderId folderId, int takeCount)
  {
    if (takeCount <= 0)
    {
      return [];
    }

    var itemView = new ItemView(takeCount)
    {
      PropertySet = new PropertySet(BasePropertySet.IdOnly)
    };

    itemView.OrderBy.Add(ItemSchema.DateTimeReceived, SortDirection.Descending);

    var findResults = service.FindItems(folderId, itemView);

    service.LoadPropertiesForItems(findResults, new PropertySet(
      BasePropertySet.FirstClassProperties,
      ItemSchema.Subject,
      ItemSchema.DateTimeReceived,
      EmailMessageSchema.InternetMessageId,
      EmailMessageSchema.From,
      ItemSchema.Body));

    var messages = new List<MailMessage>(findResults.Items.Count);

    foreach (var item in findResults)
    {
      if (item is not EmailMessage email)
      {
        continue;
      }

      messages.Add(MapToMailMessage(email));
    }

    return messages;
  }

  private static MailMessageIdentifier MapToIdentifier(EmailMessage email)
    => new(
      ExternalId: email.Id?.UniqueId ?? string.Empty,
      MessageId: email.InternetMessageId ?? string.Empty,
      Subject: email.Subject ?? string.Empty,
      From: email.From?.Address ?? email.From?.Name ?? string.Empty,
      DateUtc: email.DateTimeReceived.ToUniversalTime());

  private static MailMessage MapToMailMessage(EmailMessage email)
  {
    var body = email.Body?.Text;
    var htmlBody = email.Body?.BodyType == BodyType.HTML ? body : null;
    var textBody = email.Body?.BodyType == BodyType.Text ? body : null;

    return new MailMessage(
      ExternalId: email.Id?.UniqueId ?? string.Empty,
      MessageId: email.InternetMessageId ?? string.Empty,
      Subject: email.Subject ?? string.Empty,
      From: email.From?.Address ?? email.From?.Name ?? string.Empty,
      DateUtc: email.DateTimeReceived.ToUniversalTime(),
      HtmlBody: htmlBody,
      TextBody: textBody);
  }
}
