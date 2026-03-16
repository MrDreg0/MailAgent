using AutoFixture;
using MailAgent.Application;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace MailAgent.Mail.Tests;

[TestFixture]
public class DependencyInjectionTests
{
  private Fixture _fixture = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
  }

  [Test]
  public void AddImapMailClient_RegistersImapImplementation()
  {
    // Given.
    var services = new ServiceCollection();
    var settings = new Imap.Settings
    {
      Username = _fixture.Create<string>(),
      Password = _fixture.Create<string>(),
      Host = "imap.example.com",
      Port = 993,
      Security = SecureSocketOptions.SslOnConnect
    };

    // When.
    services.AddImapMailClient(settings);
    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    var mailClient = serviceProvider.GetRequiredService<IMailClient>();
    Assert.That(mailClient, Is.TypeOf<Imap.MailClient>());
  }

  [Test]
  public void AddEwsMailClient_RegistersEwsImplementation()
  {
    // Given.
    var services = new ServiceCollection();
    var settings = new Ews.Settings
    {
      Username = _fixture.Create<string>(),
      Password = _fixture.Create<string>(),
      Url = "https://mail.example.com/EWS/Exchange.asmx",
      Domain = "EXAMPLE"
    };

    // When.
    services.AddEwsMailClient(settings);
    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    var mailClient = serviceProvider.GetRequiredService<IMailClient>();
    Assert.That(mailClient, Is.TypeOf<Ews.MailClient>());
  }

  [Test]
  public void AddImapMailClient_Throws_WhenSettingsAreNull()
  {
    // Given.
    var services = new ServiceCollection();

    // When.
    var act = () => services.AddImapMailClient(null!);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentNullException>());
  }

  [Test]
  public void AddEwsMailClient_Throws_WhenSettingsAreNull()
  {
    // Given.
    var services = new ServiceCollection();

    // When.
    var act = () => services.AddEwsMailClient(null!);

    // Then.
    Assert.That(act, Throws.TypeOf<ArgumentNullException>());
  }
}
