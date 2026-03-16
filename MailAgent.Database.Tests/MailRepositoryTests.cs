using AutoFixture;
using MailAgent.Application;
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
