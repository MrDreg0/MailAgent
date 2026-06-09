using MailAgent.Api;
using MailAgent.Api.Configuration;
using MailAgent.Api.Endpoints;
using MailAgent.Application;
using MailAgent.Database.PostgreSql;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

webApplicationBuilder.Logging.AddSimpleConsole(options =>
{
  options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
  options.UseUtcTimestamp = true;
  options.IncludeScopes = false;
  options.SingleLine = false;
  options.ColorBehavior = LoggerColorBehavior.Disabled;
});

webApplicationBuilder.Services
  .AddProblemDetails(options =>
  {
    options.CustomizeProblemDetails = context =>
    {
      var exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

      if (exception is null)
      {
        return;
      }

      context.ProblemDetails.Detail = exception.Message;
    };
  });

webApplicationBuilder.Services
  .AddValidatedConfiguration(webApplicationBuilder.Configuration)
  .AddPostgreSqlDataContext(serviceProvider => serviceProvider.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value.Database!)
  .AddApplication()
  .AddConfiguredMailClient()
  .AddMailImportBackgroundService()
  .AddDailyDigestBackgroundService();

var webApplication = webApplicationBuilder.Build();

webApplication.UseExceptionHandler();
webApplication.MapMailEndpoints();

webApplication.Run();
