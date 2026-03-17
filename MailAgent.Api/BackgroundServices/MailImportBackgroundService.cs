using MailAgent.Application.Import;

namespace MailAgent.Api.BackgroundServices;

internal sealed class MailImportBackgroundService(
  IServiceScopeFactory serviceScopeFactory,
  MailImportBackgroundSettings settings,
  ILogger<MailImportBackgroundService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!settings.Enabled)
    {
      logger.LogInformation("Mail import background service is disabled.");
      return;
    }

    if (settings.RunOnStartup)
    {
      await RunImportCycle(stoppingToken);
    }

    using var timer = new PeriodicTimer(settings.Interval);

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      await RunImportCycle(stoppingToken);
    }
  }

  private async Task RunImportCycle(CancellationToken cancellationToken)
  {
    foreach (var folder in settings.Folders)
    {
      try
      {
        using var scope = serviceScopeFactory.CreateScope();
        var mailImportService = scope.ServiceProvider.GetRequiredService<MailImportService>();
        var result = await mailImportService.ImportFromPeriod(folder, settings.LookbackPeriod, cancellationToken);

        logger.LogInformation(
          "Imported {ImportedCount} mails from folder '{Folder}' for period {LookbackPeriod}.",
          result.Total,
          folder,
          settings.LookbackPeriod);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        logger.LogError(
          exception,
          "Mail import failed for folder '{Folder}' with lookback period {LookbackPeriod}.",
          folder,
          settings.LookbackPeriod);
      }
    }
  }
}
