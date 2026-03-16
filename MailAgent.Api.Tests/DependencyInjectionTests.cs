using AutoFixture;
using MailAgent.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MailAgent.Api.Tests;

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
  public void AddConfiguredMailClient_RegistersImapMailClient_WhenProviderIsImap()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailServer:Provider"] = "imap",
      ["MailServer:Username"] = _fixture.Create<string>(),
      ["MailServer:Password"] = _fixture.Create<string>(),
      ["MailServer:Imap:Host"] = "imap.example.com",
      ["MailServer:Imap:Port"] = "993",
      ["MailServer:Imap:Security"] = "SslOnConnect",
    });

    // When.
    services.AddConfiguredMailClient(configuration);
    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    var mailClient = serviceProvider.GetService<IMailClient>();
    Assert.That(mailClient, Is.Not.Null);
    Assert.That(mailClient!.GetType().FullName, Is.EqualTo("MailAgent.Mail.Imap.MailClient"));
  }

  [Test]
  public void AddConfiguredMailClient_Throws_WhenProviderIsUnsupported()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailServer:Provider"] = "smtp",
      ["MailServer:Username"] = _fixture.Create<string>(),
      ["MailServer:Password"] = _fixture.Create<string>(),
    });

    // When.
    var act = () => services.AddConfiguredMailClient(configuration);

    // Then.
    Assert.That(act, Throws.TypeOf<InvalidOperationException>()
      .With.Message.Contains("Unsupported mail provider"));
  }

  private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }
}
