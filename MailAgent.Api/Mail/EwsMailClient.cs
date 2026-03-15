using MailAgent.Application;
using MailAgent.Domain.Mail;
using MailAgent.Settings;
using Microsoft.Exchange.WebServices.Data;
using Task = System.Threading.Tasks.Task;
namespace MailAgent.Mail;

public sealed class EwsMailClient(EwsSettings ewsSettings) : IMailClient
{
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

  private ExchangeService CreateService()
  {
    var service = new ExchangeService(ExchangeVersion.Exchange2013_SP1)
    {
      Credentials = string.IsNullOrWhiteSpace(ewsSettings.Domain)
        ? new WebCredentials(ewsSettings.Username, ewsSettings.Password)
        : new WebCredentials(ewsSettings.Username, ewsSettings.Password, ewsSettings.Domain)
    };

    if (!string.IsNullOrWhiteSpace(ewsSettings.Url))
    {
      service.Url = new Uri(ewsSettings.Url);
      return service;
    }

    service.AutodiscoverUrl(ewsSettings.Username, redirectUrl =>
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

      var body = email.Body?.Text;
      var htmlBody = email.Body?.BodyType == BodyType.HTML ? body : null;
      var textBody = email.Body?.BodyType == BodyType.Text ? body : null;

      messages.Add(new MailMessage(
        ExternalId: email.Id?.UniqueId ?? string.Empty,
        MessageId: email.InternetMessageId ?? string.Empty,
        Subject: email.Subject ?? string.Empty,
        From: email.From?.Address ?? email.From?.Name ?? string.Empty,
        DateUtc: email.DateTimeReceived.ToUniversalTime(),
        HtmlBody: htmlBody,
        TextBody: textBody));
    }

    return messages;
  }
}
