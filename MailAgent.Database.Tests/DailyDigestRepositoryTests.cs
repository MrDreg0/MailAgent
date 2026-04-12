using MailAgent.Application.Contracts.Digest.Models;
using Microsoft.EntityFrameworkCore;

namespace MailAgent.Database.Tests;

[TestFixture]
public class DailyDigestRepositoryTests
{
  private DataContext _dbContext = null!;
  private DailyDigestRepository _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _dbContext = CreateDataContext();
    _sut = new DailyDigestRepository(_dbContext);
  }

  [TearDown]
  public void TearDown()
  {
    _dbContext.Dispose();
  }

  [Test]
  public async Task Save_PersistsDigest()
  {
    // Given.
    var digest = CreateDailyDigest();

    // When.
    await _sut.Save(digest);

    // Then.
    var savedDigest = await _dbContext.DailyDigests.SingleAsync();

    Assert.Multiple(() =>
    {
      Assert.That(savedDigest.Folder, Is.EqualTo("Releases"));
      Assert.That(savedDigest.DigestDate, Is.EqualTo(new DateOnly(2026, 4, 10)));
      Assert.That(savedDigest.DigestMarkdown, Is.EqualTo("digest body"));
    });
  }

  [Test]
  public async Task Save_UpdatesExistingDigestForSameFolderAndDate()
  {
    // Given.
    await _sut.Save(CreateDailyDigest(selected: 1, digestMarkdown: "first"));

    // When.
    await _sut.Save(CreateDailyDigest(selected: 3, digestMarkdown: "updated"));

    // Then.
    var digests = await _dbContext.DailyDigests.ToListAsync();

    Assert.That(digests, Has.Count.EqualTo(1));
    Assert.Multiple(() =>
    {
      Assert.That(digests[0].Selected, Is.EqualTo(3));
      Assert.That(digests[0].DigestMarkdown, Is.EqualTo("updated"));
    });
  }

  [Test]
  public async Task GetByDate_ReturnsDigest()
  {
    // Given.
    await _sut.Save(CreateDailyDigest());

    // When.
    var result = await _sut.GetByDate("Releases", new DateOnly(2026, 4, 10));

    // Then.
    Assert.That(result, Is.Not.Null);
    Assert.Multiple(() =>
    {
      Assert.That(result!.DigestMarkdown, Is.EqualTo("digest body"));
      Assert.That(result.Selected, Is.EqualTo(2));
    });
  }

  [Test]
  public async Task GetCount_ReturnsCountForRequestedFolderOnly()
  {
    // Given.
    await _sut.Save(CreateDailyDigest(folder: "Releases", digestDate: new DateOnly(2026, 4, 9)));
    await _sut.Save(CreateDailyDigest(folder: "Releases", digestDate: new DateOnly(2026, 4, 10)));
    await _sut.Save(CreateDailyDigest(folder: "Inbox", digestDate: new DateOnly(2026, 4, 10)));

    // When.
    var result = await _sut.GetCount("Releases");

    // Then.
    Assert.That(result, Is.EqualTo(2));
  }

  [Test]
  public async Task GetPage_ReturnsRequestedFolderPageInDescendingDateOrder()
  {
    // Given.
    await _sut.Save(CreateDailyDigest(folder: "Releases", digestDate: new DateOnly(2026, 4, 8), digestMarkdown: "oldest"));
    await _sut.Save(CreateDailyDigest(folder: "Releases", digestDate: new DateOnly(2026, 4, 9), digestMarkdown: "middle"));
    await _sut.Save(CreateDailyDigest(folder: "Releases", digestDate: new DateOnly(2026, 4, 10), digestMarkdown: "newest"));
    await _sut.Save(CreateDailyDigest(folder: "Inbox", digestDate: new DateOnly(2026, 4, 11), digestMarkdown: "other-folder"));

    // When.
    var result = await _sut.GetPage("Releases", skip: 1, take: 2);

    // Then.
    Assert.That(result.Select(x => x.DigestDate), Is.EqualTo(new[]
    {
      new DateOnly(2026, 4, 9),
      new DateOnly(2026, 4, 8)
    }));
  }

  [Test]
  public async Task GetLatest_ReturnsLatestDigestsInDescendingDateOrder()
  {
    // Given.
    await _sut.Save(CreateDailyDigest(digestDate: new DateOnly(2026, 4, 9), digestMarkdown: "older"));
    await _sut.Save(CreateDailyDigest(digestDate: new DateOnly(2026, 4, 10), digestMarkdown: "newer"));

    // When.
    var result = await _sut.GetLatest("Releases", 10);

    // Then.
    Assert.That(result.Select(x => x.DigestDate), Is.EqualTo(new[]
    {
      new DateOnly(2026, 4, 10),
      new DateOnly(2026, 4, 9)
    }));
  }

  private static DailyDigest CreateDailyDigest(
    string folder = "Releases",
    DateOnly? digestDate = null,
    int selected = 2,
    string digestMarkdown = "digest body")
  {
    return new DailyDigest(
      Id: 0,
      Folder: folder,
      DigestDate: digestDate ?? new DateOnly(2026, 4, 10),
      TotalFetched: 5,
      Selected: selected,
      DigestMarkdown: digestMarkdown,
      GeneratedAtUtc: DateTimeOffset.Parse("2026-04-11T08:00:00Z"));
  }

  private static DataContext CreateDataContext()
  {
    var options = new DbContextOptionsBuilder<DataContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    return new DataContext(options);
  }
}
