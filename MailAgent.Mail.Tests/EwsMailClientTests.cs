using AutoFixture;
using MailAgent.Application.Exceptions;

namespace MailAgent.Mail.Tests;

[TestFixture]
public class EwsMailClientTests
{
  private Fixture _fixture = null!;
  private Ews.MailClient _sut = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
    _sut = new Ews.MailClient(new Ews.Settings
    {
      Username = _fixture.Create<string>(),
      Password = _fixture.Create<string>(),
      Url = "https://mail.example.com/EWS/Exchange.asmx",
      Domain = "EXAMPLE",
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

  [Test]
  public void GetLatestFromInboxAsync_Throws_WhenEwsUrlIsMissing()
  {
    // Given.
    var sut = new Ews.MailClient(new Ews.Settings
    {
      Username = _fixture.Create<string>(),
      Password = _fixture.Create<string>(),
      Url = " ",
      Domain = "EXAMPLE",
    });

    // When.
    var act = () => sut.GetLatestFromInboxAsync(1);

    // Then.
    Assert.That(act, Throws.TypeOf<MailAgentConfigurationException>()
      .With.Message.Contains("EWS URL must be configured."));
  }
}
