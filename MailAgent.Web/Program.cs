using MailAgent.Database.PostgreSql;
using MailAgent.Web.Browse;
using MailAgent.Web.Components;
using MailAgent.Web.Configuration;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

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
