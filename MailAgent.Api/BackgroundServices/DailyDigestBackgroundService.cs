using MailAgent.Application.Digest;

namespace MailAgent.Api.BackgroundServices;

internal sealed class DailyDigestBackgroundService(
  IServiceScopeFactory serviceScopeFactory,
  DailyDigestBackgroundSettings settings,
  ILogger<DailyDigestBackgroundService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!settings.Enabled)
    {
      logger.LogInformation("Daily digest background service is disabled.");
      return;
    }

    if (settings.RunOnStartup == true)
    {
      await RunGenerationCycle(stoppingToken);
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      var delay = GetDelayUntilNextCheck(DateTimeOffset.UtcNow, settings.Interval!.Value, settings.GenerateAfter!.Value);
      await Task.Delay(delay, stoppingToken);
      await RunGenerationCycle(stoppingToken);
    }
  }

  internal static TimeSpan GetDelayUntilNextCheck(DateTimeOffset utcNow, TimeSpan interval, TimeOnly generateAfter)
  {
    var untilThreshold = generateAfter.ToTimeSpan() - utcNow.TimeOfDay;

    return untilThreshold > TimeSpan.Zero && untilThreshold < interval
      ? untilThreshold
      : interval;
  }

  private async Task RunGenerationCycle(CancellationToken cancellationToken)
  {
    var utcNow = DateTimeOffset.UtcNow;

    if (utcNow.TimeOfDay < settings.GenerateAfter!.Value.ToTimeSpan())
    {
      logger.LogInformation(
        "Skipping daily digest generation because UTC time has not reached the configured threshold yet. UtcNow={UtcNow}, GenerateAfter={GenerateAfter}.",
        utcNow,
        settings.GenerateAfter);
      return;
    }

    var digestDate = DateOnly.FromDateTime(utcNow.UtcDateTime.Date.AddDays(-1));

    try
    {
      using var scope = serviceScopeFactory.CreateScope();
      var dailyDigestGenerationService = scope.ServiceProvider.GetRequiredService<DailyDigestGenerationService>();
      var generated = await dailyDigestGenerationService.EnsureGenerated(
        settings.Folder!,
        digestDate,
        cancellationToken);

      logger.LogInformation(
        "Daily digest cycle finished. Folder={Folder}, DigestDate={DigestDate}, Generated={Generated}.",
        settings.Folder,
        digestDate,
        generated);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      logger.LogError(
        exception,
        "Daily digest cycle failed. Folder={Folder}, DigestDate={DigestDate}.",
        settings.Folder,
        digestDate);
    }
  }
}
