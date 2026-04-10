using MailAgent.Api;
using MailAgent.Api.Endpoints;
using MailAgent.Application;
using MailAgent.Database.PostgreSql;
using Microsoft.Extensions.Logging.Console;

var webApplicationBuilder = WebApplication.CreateBuilder(args);
var configuration = webApplicationBuilder.Configuration;
var connectionString = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection string is missing");

webApplicationBuilder.Logging.AddSimpleConsole(options =>
{
  options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
  options.UseUtcTimestamp = true;
  options.IncludeScopes = false;
  options.SingleLine = false;
  options.ColorBehavior = LoggerColorBehavior.Disabled;
});

webApplicationBuilder.Services
  .AddPostgreSqlDataContext(connectionString)
  .AddApplication(Settings.GetLlmSettings(configuration))
  .AddConfiguredMailClient(configuration)
  .AddMailImportBackgroundService(Settings.GetMailImportBackgroundSettings(configuration));

var webApplication = webApplicationBuilder.Build();

webApplication.MapMailEndpoints();

webApplication.Run();
