using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Contracts.Mail.Models;
using MailAgent.Application.Exceptions;
using Microsoft.Exchange.WebServices.Data;

namespace MailAgent.Mail.Ews;

public sealed class MailClient(Settings settings) : IMailClient
{
  private const int PageSize = 200;
  private const int MaxConcurrentMessageLoads = 4;

  public async Task<IReadOnlyList<string>> GetInboxSubfolderNamesAsync(CancellationToken cancellationToken = default)
  {
    var service = CreateService();

    var folderView = new FolderView(1000)
    {
      Traversal = FolderTraversal.Shallow
    };

    var folders = await service.FindFolders(WellKnownFolderName.Inbox, folderView, cancellationToken);

    IReadOnlyList<string> names = folders.Folders
      .Select(folder => folder.DisplayName)
      .ToList();

    return names;
  }

  public async Task<IReadOnlyList<MailMessage>> GetLatestFromFolderAsync(string folderPath, int takeCount, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    var service = CreateService();
    var folderId = await ResolveFolderId(service, folderPath, cancellationToken);

    return await GetLatestMessages(service, folderId, takeCount, cancellationToken);
  }

  public Task<IReadOnlyList<MailMessage>> GetLatestFromInboxAsync(int takeCount, CancellationToken cancellationToken = default)
  {
    var service = CreateService();

    return GetLatestMessages(service, new FolderId(WellKnownFolderName.Inbox), takeCount, cancellationToken);
  }

  public async Task<IReadOnlyList<MailMessageIdentifier>> GetMessageIdentifiersFromFolderSince(
    string folderPath,
    DateTimeOffset fromUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

    var service = CreateService();
    var folderId = await ResolveFolderId(service, folderPath, cancellationToken);
    var searchFilter = new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, fromUtc.UtcDateTime);
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

      var findResults = await service.FindItems(folderId, searchFilter, itemView, cancellationToken);

      identifiers.AddRange(findResults.Items
        .OfType<EmailMessage>()
        .Select(MapToIdentifier));

      if (!findResults.MoreAvailable)
      {
        break;
      }

      offset += findResults.Items.Count;
    }

    return identifiers;
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

    var service = CreateService();
    var normalizedIds = externalIds
      .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
      .Distinct(StringComparer.Ordinal)
      .ToList();

    if (normalizedIds.Count == 0)
    {
      return [];
    }

    // Resolve the folder up front so callers keep the same contract as other folder-based operations.
    _ = await ResolveFolderId(service, folderPath, cancellationToken);

    var propertySet = new PropertySet(
      BasePropertySet.FirstClassProperties,
      ItemSchema.Subject,
      ItemSchema.DateTimeReceived,
      EmailMessageSchema.InternetMessageId,
      EmailMessageSchema.From,
      ItemSchema.Body);

    var semaphore = new SemaphoreSlim(MaxConcurrentMessageLoads, MaxConcurrentMessageLoads);

    try
    {
      var loadTasks = new List<Task<EmailMessage>>(normalizedIds.Count);
      
      loadTasks.AddRange(
        normalizedIds.Select(
          externalId => LoadMessage(externalId, service, propertySet, semaphore, cancellationToken)));

      var loadedMessages = await System.Threading.Tasks.Task.WhenAll(loadTasks);

      return loadedMessages.Select(MapToMailMessage).ToList();
    }
    finally
    {
      semaphore.Dispose();
    }
  }

  private ExchangeService CreateService()
  {
    if (string.IsNullOrWhiteSpace(settings.Url))
    {
      throw new MailAgentConfigurationException("EWS URL must be configured.");
    }

    var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
    {
      Url = new Uri(settings.Url),
      Credentials = string.IsNullOrWhiteSpace(settings.Domain)
        ? new WebCredentials(settings.Username, settings.Password)
        : new WebCredentials(settings.Username, settings.Password, settings.Domain)
    };

    return service;
  }

  private static async Task<FolderId> ResolveFolderId(ExchangeService service, string folderPath, CancellationToken cancellationToken)
  {
    var currentFolderId = new FolderId(WellKnownFolderName.Inbox);
    var segments = folderPath.Split(['/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    foreach (var segment in segments)
    {
      var folderView = new FolderView(1000)
      {
        Traversal = FolderTraversal.Shallow
      };

      var childFolders = await service.FindFolders(currentFolderId, folderView, cancellationToken);
      var nextFolder = childFolders.Folders.FirstOrDefault(folder =>
        folder.DisplayName.Equals(segment, StringComparison.OrdinalIgnoreCase));

      if (nextFolder is null)
      {
        throw new MailFolderNotFoundException(folderPath);
      }

      currentFolderId = nextFolder.Id;
    }

    return currentFolderId;
  }

  private static async Task<IReadOnlyList<MailMessage>> GetLatestMessages(
    ExchangeService service,
    FolderId folderId,
    int takeCount,
    CancellationToken cancellationToken)
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

    var findResults = await service.FindItems(folderId, itemView, cancellationToken);

    await service.LoadPropertiesForItems(findResults, new PropertySet(
      BasePropertySet.FirstClassProperties,
      ItemSchema.Subject,
      ItemSchema.DateTimeReceived,
      EmailMessageSchema.InternetMessageId,
      EmailMessageSchema.From,
      ItemSchema.Body), cancellationToken);

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
      ExternalId: email.Id.UniqueId ?? string.Empty,
      MessageId: email.InternetMessageId,
      Subject: email.Subject,
      From: email.From.Address,
      DateUtc: email.DateTimeReceived.ToUniversalTime());

  private static async Task<EmailMessage> LoadMessage(
    string externalId,
    ExchangeService service,
    PropertySet propertySet,
    SemaphoreSlim semaphore,
    CancellationToken cancellationToken)
  {
    await semaphore.WaitAsync(cancellationToken);

    try
    {
      return await EmailMessage.Bind(service, new ItemId(externalId), propertySet, cancellationToken);
    }
    finally
    {
      semaphore.Release();
    }
  }

  private static MailMessage MapToMailMessage(EmailMessage email)
  {
    var body = email.Body.Text;
    var htmlBody = email.Body.BodyType == BodyType.HTML ? body : null;
    var textBody = email.Body.BodyType == BodyType.Text ? body : null;

    return new MailMessage(
      ExternalId: email.Id.UniqueId ?? string.Empty,
      MessageId: email.InternetMessageId,
      Subject: email.Subject,
      From: email.From.Address,
      DateUtc: email.DateTimeReceived.ToUniversalTime(),
      HtmlBody: htmlBody,
      TextBody: textBody);
  }
}
