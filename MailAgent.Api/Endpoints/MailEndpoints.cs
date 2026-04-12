using MailAgent.Api.Models;
using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Digest;
using MailAgent.Application.Import;
namespace MailAgent.Api.Endpoints;

public static class MailEndpoints
{
  public static void MapMailEndpoints(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapGet("/folders", GetFolders);
    endpoints.MapGet("/mails", GetMails);
    endpoints.MapPost("/mails/import", ImportMails);
    endpoints.MapGet("/digest", GetDigest);
    endpoints.MapGet("/daily-digests", GetDailyDigests);
    endpoints.MapGet("/daily-digests/{digestDate}", GetDailyDigestByDate);
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

  private static async Task<IResult> ImportMails(ImportMailsRequest request, MailImportService mailImportService, CancellationToken cancellationToken)
  {
    var result = await mailImportService.ImportLatestFromFolderAsync(request.Folder, request.TakeCount, cancellationToken);
    
    return Results.Ok(new { result.Total, result.Latest });
  }

  private static async Task<IResult> GetDigest(string folder, string period, ReleaseDigestService releaseDigestService, CancellationToken cancellationToken)
  {
    var result = await releaseDigestService.BuildInboxDigestAsync(folder, TimeSpan.Parse(period), cancellationToken);
    return Results.Ok(result);
  }

  private static async Task<IResult> GetDailyDigests(
    string folder,
    int? take,
    IDailyDigestRepository dailyDigestRepository,
    CancellationToken cancellationToken)
  {
    var digests = await dailyDigestRepository.GetLatest(folder, take ?? 30, cancellationToken);
    return Results.Ok(new { Items = digests });
  }

  private static async Task<IResult> GetDailyDigestByDate(
    DateOnly digestDate,
    string folder,
    IDailyDigestRepository dailyDigestRepository,
    CancellationToken cancellationToken)
  {
    var digest = await dailyDigestRepository.GetByDate(folder, digestDate, cancellationToken);
    return digest is null ? Results.NotFound() : Results.Ok(digest);
  }
}
