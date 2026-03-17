using MailAgent.Application.Import;
using MailAgent.Application.Contracts.Mail;

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
      DateTimeOffset? latestDateUtc = null;
      var fromUtc = DateTimeOffset.UtcNow.Subtract(settings.InitialLookbackPeriod);

      try
      {
        using var scope = serviceScopeFactory.CreateScope();
        var mailImportService = scope.ServiceProvider.GetRequiredService<MailImportService>();
        var mailRepository = scope.ServiceProvider.GetRequiredService<IMailRepository>();
        latestDateUtc = await mailRepository.GetLatestDateUtcByFolder(folder, cancellationToken);
        fromUtc = latestDateUtc?.Subtract(settings.OverlapPeriod) ?? fromUtc;
        var result = await mailImportService.ImportFromDate(folder, fromUtc, cancellationToken);

        logger.LogInformation(
          "Mail import for folder '{Folder}': latestDateUtc={LatestDateUtc}, fromUtc={FromUtc}, identifiersFound={IdentifiersFound}, alreadyStored={AlreadyStored}, loaded={Loaded}, saveCandidates={SaveCandidates}, imported={ImportedCount}.",
          folder,
          latestDateUtc,
          fromUtc,
          result.IdentifiersFound,
          result.AlreadyStored,
          result.Loaded,
          result.SaveCandidates,
          result.Total);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception exception)
      {
        logger.LogError(
          exception,
          "Mail import failed for folder '{Folder}' since {FromUtc}.",
          folder,
          fromUtc);
      }
    }
  }
}
