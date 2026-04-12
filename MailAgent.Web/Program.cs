using MailAgent.Api.Contracts;
using MailAgent.Database.PostgreSql;
using MailAgent.Web.Browse;
using MailAgent.Web.Components;
using MailAgent.Web.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Refit;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
  options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
  options.UseUtcTimestamp = true;
  options.IncludeScopes = false;
  options.SingleLine = false;
  options.ColorBehavior = LoggerColorBehavior.Disabled;
});

builder.Services.AddBlazorBootstrap();

builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

builder.Services
  .AddValidatedConfiguration(builder.Configuration)
  .AddPostgreSqlDataContext(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value.Database!)
  .AddScoped<MailBrowserService>()
  .AddScoped<DailyDigestBrowserService>();

builder.Services.AddRefitClient<IMailAgentApi>()
  .ConfigureHttpClient((serviceProvider, client) =>
  {
    var apiConfiguration = serviceProvider.GetRequiredService<IOptions<MailAgentApiConfiguration>>().Value;
    client.BaseAddress = new Uri(apiConfiguration.BaseUrl!);
    client.Timeout = TimeSpan.FromMinutes(apiConfiguration.TimeoutMinutes!.Value);
  });

var app = builder.Build();
var useHttpsRedirection = bool.Parse(app.Services.GetRequiredService<IOptions<WebHostConfiguration>>().Value.UseHttpsRedirection!);

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

if (useHttpsRedirection)
{
  app.UseHttpsRedirection();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();
