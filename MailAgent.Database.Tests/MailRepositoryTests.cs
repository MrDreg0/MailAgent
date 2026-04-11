using AutoFixture;
using MailAgent.Application.Contracts.Mail.Models;
using Microsoft.EntityFrameworkCore;

namespace MailAgent.Database.Tests;

[TestFixture]
public class MailRepositoryTests
{
  private Fixture _fixture = null!;
  private DataContext _dbContext = null!;
  private MailRepository _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _dbContext = CreateDataContext();
    _sut = new MailRepository(_dbContext);
  }

  [TearDown]
  public void TearDown()
  {
    _dbContext.Dispose();
  }

  [Test]
  public async Task SaveNewAsync_InsertsOnlyUnseenMessages_AndDeduplicatesIncomingBatch()
  {
    // Given.
    _dbContext.Mails.Add(new MailRecord(
      Id: 1,
      Folder: "existing-folder",
      MessageId: "existing-message",
      DateUtc: DateTimeOffset.Parse("2026-03-16T10:00:00Z"),
      From: "existing@example.com",
      Subject: "Existing subject",
      RawBody: "existing raw",
      MarkdownBody: "existing markdown",
      InsertedAt: "2026-03-16 10:00:00Z"));
    await _dbContext.SaveChangesAsync();

    var mails = new[]
    {
      CreateStoredMail(messageId: "existing-message"),
      CreateStoredMail(messageId: "NEW-message", subject: "First new"),
      CreateStoredMail(messageId: "new-MESSAGE", subject: "Duplicate new")
    };

    // When.
    await _sut.SaveNewAsync(mails);

    // Then.
    var savedRecords = await _dbContext.Mails
      .OrderBy(x => x.MessageId)
      .ToListAsync();

    Assert.That(savedRecords, Has.Count.EqualTo(2));
    Assert.That(savedRecords.Select(x => x.MessageId), Is.EquivalentTo(["existing-message", "NEW-message"]));
    Assert.That(savedRecords.Single(x => x.MessageId == "NEW-message").Subject, Is.EqualTo("First new"));
  }

  [Test]
  public async Task SaveNewAsync_MapsStoredMailFieldsToMailRecord()
  {
    // Given.
    var mail = CreateStoredMail(
      folder: "Releases",
      messageId: "message-id",
      from: "from@example.com",
      subject: "Release",
      rawBody: "raw body",
      markdownBody: "markdown body",
      insertedAt: "2026-03-16 11:00:00Z");

    // When.
    await _sut.SaveNewAsync([mail]);

    // Then.
    var record = await _dbContext.Mails.SingleAsync();
    Assert.Multiple(() =>
    {
      Assert.That(record.Folder, Is.EqualTo(mail.Folder));
      Assert.That(record.MessageId, Is.EqualTo(mail.MessageId));
      Assert.That(record.DateUtc, Is.EqualTo(mail.DateUtc));
      Assert.That(record.From, Is.EqualTo(mail.From));
      Assert.That(record.Subject, Is.EqualTo(mail.Subject));
      Assert.That(record.RawBody, Is.EqualTo(mail.RawBody));
      Assert.That(record.MarkdownBody, Is.EqualTo(mail.MarkdownBody));
      Assert.That(record.InsertedAt, Is.EqualTo(mail.InsertedAt));
    });
  }

  [Test]
  public async Task GetLatestFromFolderAsync_ReturnsLatestMailsForFolderInDescendingDateOrder()
  {
    // Given.
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", DateTimeOffset.Parse("2026-03-16T08:00:00Z"), "a@example.com", "older", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "b@example.com", "newest", "raw-2", "md-2", "2026-03-16 10:00:00Z"),
      new MailRecord(3, "Other", "message-3", DateTimeOffset.Parse("2026-03-16T12:00:00Z"), "c@example.com", "other-folder", "raw-3", "md-3", "2026-03-16 12:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetLatestFromFolderAsync("Releases", 2);

    // Then.
    Assert.That(result.Select(x => x.MessageId), Is.EqualTo(["message-2", "message-1"]));
    Assert.That(result.All(x => x.Folder == "Releases"), Is.True);
  }

  [Test]
  public async Task GetLatestFromFolderAsync_ReturnsEmptyList_WhenTakeCountIsNotPositive()
  {
    // When.
    var result = await _sut.GetLatestFromFolderAsync("Releases", 0);

    // Then.
    Assert.That(result, Is.Empty);
  }

  [Test]
  public void GetLatestFromFolderAsync_Throws_WhenFolderNameIsEmpty()
  {
    // When.
    var act = () => _sut.GetLatestFromFolderAsync(" ", 1);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentException>());
  }

  [Test]
  public async Task GetByPeriodFromFolder_ReturnsOnlyMailsFromRequestedFolderWithinPeriod()
  {
    // Given.
    var now = DateTimeOffset.UtcNow;
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", now - TimeSpan.FromMinutes(30), "a@example.com", "recent", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", now - TimeSpan.FromHours(3), "b@example.com", "old", "raw-2", "md-2", "2026-03-16 10:00:00Z"),
      new MailRecord(3, "Other", "message-3", now - TimeSpan.FromMinutes(20), "c@example.com", "other-folder", "raw-3", "md-3", "2026-03-16 12:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetByPeriodFromFolder("Releases", TimeSpan.FromHours(1));

    // Then.
    Assert.That(result.Select(x => x.MessageId), Is.EqualTo(["message-1"]));
  }

  [Test]
  public async Task GetByPeriodFromFolder_ReturnsEmptyList_WhenPeriodIsZero()
  {
    // When.
    var result = await _sut.GetByPeriodFromFolder("Releases", TimeSpan.Zero);

    // Then.
    Assert.That(result, Is.Empty);
  }

  [Test]
  public void GetByPeriodFromFolder_Throws_WhenFolderNameIsEmpty()
  {
    // When.
    var act = () => _sut.GetByPeriodFromFolder(" ", TimeSpan.FromHours(1));

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentException>());
  }

  [Test]
  public async Task GetPage_ReturnsRequestedSlice_InDescendingDateOrder()
  {
    // Given.
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", DateTimeOffset.Parse("2026-03-16T08:00:00Z"), "a@example.com", "first", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "b@example.com", "second", "raw-2", "md-2", "2026-03-16 10:00:00Z"),
      new MailRecord(3, "Other", "message-3", DateTimeOffset.Parse("2026-03-16T12:00:00Z"), "c@example.com", "third", "raw-3", "md-3", "2026-03-16 12:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetPage(skip: 1, take: 2);

    // Then.
    Assert.That(result.Select(x => x.MessageId), Is.EqualTo(["message-2", "message-1"]));
  }

  [Test]
  public async Task GetPage_UsesIdAsTieBreaker_WhenDatesAreEqual()
  {
    // Given.
    var sameDateUtc = DateTimeOffset.Parse("2026-03-16T10:00:00Z");
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", sameDateUtc, "a@example.com", "first", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", sameDateUtc, "b@example.com", "second", "raw-2", "md-2", "2026-03-16 10:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetPage(skip: 0, take: 2);

    // Then.
    Assert.That(result.Select(x => x.Id), Is.EqualTo([2, 1]));
  }

  [Test]
  public void GetPage_Throws_WhenSkipIsNegative()
  {
    // When.
    var act = () => _sut.GetPage(skip: -1, take: 10);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentOutOfRangeException>()
      .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("skip"));
  }

  [Test]
  public async Task GetPage_ReturnsEmptyList_WhenTakeIsNotPositive()
  {
    // When.
    var result = await _sut.GetPage(skip: 0, take: 0);

    // Then.
    Assert.That(result, Is.Empty);
  }

  [Test]
  public async Task GetCount_ReturnsTotalStoredMailCount()
  {
    // Given.
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", DateTimeOffset.Parse("2026-03-16T08:00:00Z"), "a@example.com", "first", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Other", "message-2", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "b@example.com", "second", "raw-2", "md-2", "2026-03-16 10:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetCount();

    // Then.
    Assert.That(result, Is.EqualTo(2));
  }

  [Test]
  public async Task GetExistingMessageIds_ReturnsOnlyExistingIds_IgnoringCase()
  {
    // Given.
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", DateTimeOffset.Parse("2026-03-16T08:00:00Z"), "a@example.com", "older", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "b@example.com", "newest", "raw-2", "md-2", "2026-03-16 10:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetExistingMessageIds(["MESSAGE-1", "missing-message", "message-2"]);

    // Then.
    Assert.That(result, Is.EquivalentTo(["message-1", "message-2"]));
  }

  [Test]
  public async Task GetExistingMessageIds_ReturnsEmptySet_WhenInputContainsOnlyEmptyValues()
  {
    // When.
    var result = await _sut.GetExistingMessageIds([" ", "\t"]);

    // Then.
    Assert.That(result, Is.Empty);
  }

  [Test]
  public async Task GetLatestDateUtcByFolder_ReturnsLatestDateForFolder()
  {
    // Given.
    _dbContext.Mails.AddRange(
      new MailRecord(1, "Releases", "message-1", DateTimeOffset.Parse("2026-03-16T08:00:00Z"), "a@example.com", "older", "raw-1", "md-1", "2026-03-16 08:00:00Z"),
      new MailRecord(2, "Releases", "message-2", DateTimeOffset.Parse("2026-03-16T10:00:00Z"), "b@example.com", "newest", "raw-2", "md-2", "2026-03-16 10:00:00Z"),
      new MailRecord(3, "Other", "message-3", DateTimeOffset.Parse("2026-03-16T12:00:00Z"), "c@example.com", "other-folder", "raw-3", "md-3", "2026-03-16 12:00:00Z"));
    await _dbContext.SaveChangesAsync();

    // When.
    var result = await _sut.GetLatestDateUtcByFolder("Releases");

    // Then.
    Assert.That(result, Is.EqualTo(DateTimeOffset.Parse("2026-03-16T10:00:00Z")));
  }

  [Test]
  public void GetLatestDateUtcByFolder_Throws_WhenFolderNameIsEmpty()
  {
    // When.
    var act = () => _sut.GetLatestDateUtcByFolder(" ");

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentException>());
  }

  private DataContext CreateDataContext()
  {
    var options = new DbContextOptionsBuilder<DataContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    return new DataContext(options);
  }

  private StoredMail CreateStoredMail(
    string? folder = null,
    string? messageId = null,
    string? from = null,
    string? subject = null,
    string? rawBody = null,
    string? markdownBody = null,
    string? insertedAt = null)
  {
    return _fixture.Build<StoredMail>()
      .With(x => x.Id, 0)
      .With(x => x.Folder, folder ?? _fixture.Create<string>())
      .With(x => x.MessageId, messageId ?? _fixture.Create<string>())
      .With(x => x.From, from ?? _fixture.Create<string>())
      .With(x => x.Subject, subject ?? _fixture.Create<string>())
      .With(x => x.RawBody, rawBody ?? _fixture.Create<string>())
      .With(x => x.MarkdownBody, markdownBody ?? _fixture.Create<string>())
      .With(x => x.InsertedAt, insertedAt ?? _fixture.Create<string>())
      .Create();
  }
}
