using AutoFixture;
using MailKit.Security;

namespace MailAgent.Mail.Tests;

[TestFixture]
public class ImapMailClientTests
{
  private Fixture _fixture = null!;
  private Imap.MailClient _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _sut = new Imap.MailClient(new Imap.Settings
    {
      Username = _fixture.Create<string>(),
      Password = _fixture.Create<string>(),
      Host = "imap.example.com",
      Port = 993,
      Security = SecureSocketOptions.SslOnConnect
    });
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  public void GetLatestFromFolderAsync_Throws_WhenFolderPathIsNullOrWhiteSpace(string? folderPath)
  {
    // When.
    var act = async () => await _sut.GetLatestFromFolderAsync(folderPath!, 5);

    // Then.
    Assert.That(act, Throws.InstanceOf<ArgumentException>());
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  public void GetMessageIdentifiersFromFolderSince_Throws_WhenFolderPathIsNullOrWhiteSpace(string? folderPath)
  {
    // When.
    var act = async () => await _sut.GetMessageIdentifiersFromFolderSince(folderPath!, DateTimeOffset.UtcNow);

    // Then.
    Assert.That(act, Throws.InstanceOf<ArgumentException>());
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  public void GetMessagesByExternalIds_Throws_WhenFolderPathIsNullOrWhiteSpace(string? folderPath)
  {
    // When.
    var act = async () => await _sut.GetMessagesByExternalIds(folderPath!, ["id-1"]);

    // Then.
    Assert.That(act, Throws.InstanceOf<ArgumentException>());
  }

  [Test]
  public async Task GetMessagesByExternalIds_ReturnsEmptyList_WhenExternalIdsAreEmpty()
  {
    // When.
    var result = await _sut.GetMessagesByExternalIds("Releases", []);

    // Then.
    Assert.That(result, Is.Empty);
  }
}
