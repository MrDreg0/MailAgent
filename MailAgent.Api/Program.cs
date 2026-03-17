using MailAgent.Api;
using MailAgent.Api.Endpoints;
using MailAgent.Application;
using MailAgent.Database.PostgreSql;

var webApplicationBuilder = WebApplication.CreateBuilder(args);
var configuration = webApplicationBuilder.Configuration;
var connectionString = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Database connection string is missing");

webApplicationBuilder.Services
  .AddPostgreSqlDataContext(connectionString)
  .AddApplication(Settings.GetOllamaSettings(configuration))
  .AddConfiguredMailClient(configuration)
  .AddMailImportBackgroundService(Settings.GetMailImportBackgroundSettings(configuration));

var webApplication = webApplicationBuilder.Build();

webApplication.MapMailEndpoints();

webApplication.Run();
