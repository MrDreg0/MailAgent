using MailAgent.Database.PostgreSql;
using MailAgent.Web.Browse;
using MailAgent.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Database")
  ?? throw new InvalidOperationException("Database connection string is missing");

builder.Services.AddBlazorBootstrap();

builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

builder.Services
  .AddPostgreSqlDataContext(connectionString)
  .AddScoped<MailBrowserService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();
