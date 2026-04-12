using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using Microsoft.Extensions.Logging;

namespace MailAgent.Application.Digest;

public sealed class DailyDigestGenerationService(
  IDailyDigestRepository dailyDigestRepository,
  DailyDigestService dailyDigestService,
  ILogger<DailyDigestGenerationService> logger)
{
  public async Task<bool> EnsureGenerated(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    var existingDigest = await dailyDigestRepository.GetByDate(folderName, digestDate, cancellationToken);

    if (existingDigest is not null)
    {
      logger.LogInformation(
        "Skipping daily digest generation because a digest already exists. Folder={Folder}, DigestDate={DigestDate}.",
        folderName,
        digestDate);

      return false;
    }

    try
    {
      await BuildAndSave(folderName, digestDate, cancellationToken);

      return true;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      logger.LogError(
        exception,
        "Daily digest generation failed. Folder={Folder}, DigestDate={DigestDate}.",
        folderName,
        digestDate);

      throw;
    }
  }

  public async Task<DailyDigest> Regenerate(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    try
    {
      logger.LogInformation(
        "Force regenerating daily digest. Folder={Folder}, DigestDate={DigestDate}.",
        folderName,
        digestDate);

      return await BuildAndSave(folderName, digestDate, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      logger.LogError(
        exception,
        "Daily digest regeneration failed. Folder={Folder}, DigestDate={DigestDate}.",
        folderName,
        digestDate);

      throw;
    }
  }

  private async Task<DailyDigest> BuildAndSave(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken)
  {
    var buildResult = await dailyDigestService.BuildForDate(folderName, digestDate, cancellationToken);
    var storedDigest = new DailyDigest(
      Id: 0,
      Folder: buildResult.Folder,
      DigestDate: buildResult.DigestDate,
      TotalFetched: buildResult.TotalFetched,
      Selected: buildResult.Selected,
      DigestMarkdown: buildResult.DigestMarkdown,
      GeneratedAtUtc: DateTimeOffset.UtcNow);

    await dailyDigestRepository.Save(storedDigest, cancellationToken);

    logger.LogInformation(
      "Daily digest stored. Folder={Folder}, DigestDate={DigestDate}, TotalFetched={TotalFetched}, Selected={Selected}.",
      buildResult.Folder,
      buildResult.DigestDate,
      buildResult.TotalFetched,
      buildResult.Selected);

    return storedDigest;
  }
}
