using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Digest.Models;
using Microsoft.EntityFrameworkCore;

namespace MailAgent.Database;

public sealed class DailyDigestRepository(DataContext dbContext) : IDailyDigestRepository
{
  public Task<int> GetCount(
    string folderName,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    return dbContext.DailyDigests
      .AsNoTracking()
      .CountAsync(digest => digest.Folder == folderName, cancellationToken);
  }

  public async Task<IReadOnlyList<DailyDigest>> GetPage(
    string folderName,
    int skip,
    int take,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    if (skip < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(skip), skip, "Skip must be non-negative.");
    }

    if (take <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(take), take, "Take must be positive.");
    }

    return await dbContext.DailyDigests
      .AsNoTracking()
      .Where(digest => digest.Folder == folderName)
      .OrderByDescending(digest => digest.DigestDate)
      .ThenByDescending(digest => digest.GeneratedAtUtc)
      .Skip(skip)
      .Take(take)
      .Select(digest => new DailyDigest(
        digest.Id,
        digest.Folder,
        digest.DigestDate,
        digest.TotalFetched,
        digest.Selected,
        digest.DigestMarkdown,
        digest.GeneratedAtUtc))
      .ToListAsync(cancellationToken);
  }

  public async Task<DailyDigest?> GetByDate(
    string folderName,
    DateOnly digestDate,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    var record = await dbContext.DailyDigests
      .Where(digest => digest.Folder == folderName)
      .Where(digest => digest.DigestDate == digestDate)
      .SingleOrDefaultAsync(cancellationToken);

    return record is null ? null : Map(record);
  }

  public async Task<IReadOnlyList<DailyDigest>> GetLatest(
    string folderName,
    int takeCount,
    CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

    if (takeCount <= 0)
    {
      return [];
    }

    return await dbContext.DailyDigests
      .Where(digest => digest.Folder == folderName)
      .OrderByDescending(digest => digest.DigestDate)
      .ThenByDescending(digest => digest.GeneratedAtUtc)
      .Take(takeCount)
      .Select(digest => new DailyDigest(
        digest.Id,
        digest.Folder,
        digest.DigestDate,
        digest.TotalFetched,
        digest.Selected,
        digest.DigestMarkdown,
        digest.GeneratedAtUtc))
      .ToListAsync(cancellationToken);
  }

  public async Task Save(DailyDigest digest, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(digest.Folder);

    var existingRecord = await dbContext.DailyDigests
      .Where(record => record.Folder == digest.Folder)
      .Where(record => record.DigestDate == digest.DigestDate)
      .SingleOrDefaultAsync(cancellationToken);

    if (existingRecord is null)
    {
      existingRecord = new DailyDigestRecord();
      await dbContext.DailyDigests.AddAsync(existingRecord, cancellationToken);
    }

    existingRecord.Folder = digest.Folder;
    existingRecord.DigestDate = digest.DigestDate;
    existingRecord.TotalFetched = digest.TotalFetched;
    existingRecord.Selected = digest.Selected;
    existingRecord.DigestMarkdown = digest.DigestMarkdown;
    existingRecord.GeneratedAtUtc = digest.GeneratedAtUtc;

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  private static DailyDigest Map(DailyDigestRecord record)
  {
    return new DailyDigest(
      record.Id,
      record.Folder,
      record.DigestDate,
      record.TotalFetched,
      record.Selected,
      record.DigestMarkdown,
      record.GeneratedAtUtc);
  }
}
