using MailAgent.Application;
using MailAgent.Database.PostgreSql;
using MailAgent.Mail;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

var connectionString = webApplicationBuilder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection string is missing");
webApplicationBuilder.Services.AddPostgreSqlDataContext(connectionString);
webApplicationBuilder.Services.AddSingleton<EmailBodyConverter>();
webApplicationBuilder.Services.AddScoped<MailImportService>();
webApplicationBuilder.Services.AddScoped<ReleaseDigestService>();
webApplicationBuilder.Services.AddSingleton<OllamaClient>();
webApplicationBuilder.Services.AddMailClient(webApplicationBuilder.Configuration);

webApplicationBuilder.Services.AddHttpClient("ollama", client =>
{
  client.BaseAddress = new Uri("http://localhost:11434/");
  client.Timeout = TimeSpan.FromMinutes(5);
});

var webApplication = webApplicationBuilder.Build();
webApplication.MapGet("/folders", async (IMailClient mailClient, CancellationToken cancellationToken) =>
{
  var folderNames = await mailClient.GetInboxSubfolderNamesAsync(cancellationToken);

  return Results.Ok(new
  {
    Folders = folderNames,
  });
});

webApplication.MapGet("/mails", async (IMailClient mailClient, CancellationToken cancellationToken) =>
{
  const int takeCount = 5;
  const string folderName = "/";

  var fetchedMessages = await mailClient.GetLatestFromFolderAsync(folderName, takeCount, cancellationToken);
  var messageSummaries = new List<object>(capacity: fetchedMessages.Count);

  foreach (var message in fetchedMessages)
  {
    messageSummaries.Add(new
    {
      message.ExternalId,
      MessageId = message.NormalizedMessageId,
      message.Subject,
      message.From,
      Date = message.DateUtc.ToString("u"),
    });
  }

  return Results.Ok(new
  {
    Total = messageSummaries.Count,
    Latest = messageSummaries,
  });
});

webApplication.MapGet("/test-mail", async (MailImportService mailImportService, CancellationToken cancellationToken) =>
{
  const string folderName = "Releases";
  const int takeCount = 5;

  var result = await mailImportService.ImportLatestFromFolderAsync(folderName, takeCount, cancellationToken);

  return Results.Ok(new
  {
    result.Total,
    result.Latest,
  });
});

webApplication.MapGet("/digest", async (ReleaseDigestService releaseDigestService, CancellationToken cancellationToken) =>
{
  const int takeCount = 10;
  var result = await releaseDigestService.BuildInboxDigestAsync(takeCount, cancellationToken);

  return Results.Ok(result);
});

webApplication.Run();
