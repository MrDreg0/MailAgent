using Microsoft.EntityFrameworkCore;

namespace MailAgent.Database.Tests;

[TestFixture]
public class DataContextTests
{
  [Test]
  public void OnModelCreating_ConfiguresUniqueIndexForMessageId()
  {
    // Given.
    var options = new DbContextOptionsBuilder<DataContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    using var dbContext = new DataContext(options);

    // When.
    var entityType = dbContext.Model.FindEntityType(typeof(MailRecord));
    var index = entityType!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(MailRecord.MessageId));

    // Then.
    Assert.That(index.IsUnique, Is.True);
  }

  [Test]
  public void OnModelCreating_ConfiguresUniqueIndexForDigestFolderAndDate()
  {
    // Given.
    var options = new DbContextOptionsBuilder<DataContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    using var dbContext = new DataContext(options);

    // When.
    var entityType = dbContext.Model.FindEntityType(typeof(DailyDigestRecord));
    var index = entityType!.GetIndexes().Single(i =>
      i.Properties.Select(x => x.Name).SequenceEqual([nameof(DailyDigestRecord.Folder), nameof(DailyDigestRecord.DigestDate)]));

    // Then.
    Assert.That(index.IsUnique, Is.True);
  }
}
