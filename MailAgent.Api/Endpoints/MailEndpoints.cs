using MailAgent.Application;
namespace MailAgent.Api.Endpoints;

public static class MailEndpoints
{
  public static void MapMailEndpoints(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapGet("/folders", GetFolders);
    endpoints.MapGet("/mails", GetMails);
    endpoints.MapGet("/test-mail", ImportMails);
    endpoints.MapGet("/digest", GetDigest);
  }

  private static async Task<IResult> GetFolders(IMailClient mailClient, CancellationToken cancellationToken)
  {
    var folderNames = await mailClient.GetInboxSubfolderNamesAsync(cancellationToken);
    return Results.Ok(new { Folders = folderNames });
  }

  private static async Task<IResult> GetMails(IMailClient mailClient, CancellationToken cancellationToken)
  {
    const int takeCount = 5;
    const string folderName = "/";

    var fetchedMessages = await mailClient.GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
    var messageSummaries = fetchedMessages.Select(message => new
    {
      message.ExternalId,
      MessageId = message.NormalizedMessageId,
      message.Subject,
      message.From,
      Date = message.DateUtc.ToString("u"),
    });

    return Results.Ok(new
    {
      Total = fetchedMessages.Count,
      Latest = messageSummaries,
    });
  }

  private static async Task<IResult> ImportMails(MailImportService mailImportService, CancellationToken cancellationToken)
  {
    const string folderName = "Releases";
    const int takeCount = 5;

    var result = await mailImportService.ImportLatestFromFolderAsync(folderName, takeCount, cancellationToken);
    return Results.Ok(new { result.Total, result.Latest });
  }

  private static async Task<IResult> GetDigest(ReleaseDigestService releaseDigestService, CancellationToken cancellationToken)
  {
    const int takeCount = 10;
    var result = await releaseDigestService.BuildInboxDigestAsync(takeCount, cancellationToken);
    return Results.Ok(result);
  }
}