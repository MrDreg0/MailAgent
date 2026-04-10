using AutoFixture;
using MailAgent.Api.BackgroundServices;
using MailKit.Security;
using Microsoft.Extensions.Configuration;

namespace MailAgent.Api.Tests;

[TestFixture]
public class SettingsTests
{
  private Fixture _fixture = null!;

  [SetUp]
  public void SetUp()
  {
    _fixture = new Fixture();
  }

  [Test]
  public void GetLlmSettings_ReturnsConfiguredValues_WhenSectionExists()
  {
    // Given.
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["Llm:Provider"] = "lmstudio",
      ["Llm:BaseUrl"] = "http://localhost:1234/v1/",
      ["Llm:TimeoutMinutes"] = "12",
      ["Llm:FastModel"] = "fast-local-model",
      ["Llm:MainModel"] = "main-local-model",
    });

    // When.
    var result = Settings.GetLlmSettings(configuration);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Provider, Is.EqualTo("lmstudio"));
      Assert.That(result.BaseUrl, Is.EqualTo("http://localhost:1234/v1/"));
      Assert.That(result.Timeout, Is.EqualTo(TimeSpan.FromMinutes(12)));
      Assert.That(result.FastModel, Is.EqualTo("fast-local-model"));
      Assert.That(result.MainModel, Is.EqualTo("main-local-model"));
    });
  }

  [Test]
  public void GetLlmSettings_ReturnsDefaults_WhenSectionIsMissing()
  {
    // Given.
    var configuration = BuildConfiguration(new Dictionary<string, string?>());

    // When.
    var result = Settings.GetLlmSettings(configuration);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Provider, Is.EqualTo("ollama"));
      Assert.That(result.BaseUrl, Is.EqualTo("http://localhost:11434/"));
      Assert.That(result.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)));
      Assert.That(result.FastModel, Is.EqualTo("llama3.2:3b"));
      Assert.That(result.MainModel, Is.EqualTo("qwen2.5:7b-instruct"));
    });
  }

  [Test]
  public void CreateImapSettings_ReturnsParsedSettings_WhenConfigurationIsValid()
  {
    // Given.
    var username = _fixture.Create<string>();
    var password = _fixture.Create<string>();
    var mailServerSection = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailServer:Imap:Host"] = "imap.example.com",
      ["MailServer:Imap:Port"] = "993",
      ["MailServer:Imap:Security"] = "SslOnConnect",
    }).GetSection("MailServer");

    // When.
    var result = Settings.CreateImapSettings(mailServerSection, username, password);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Username, Is.EqualTo(username));
      Assert.That(result.Password, Is.EqualTo(password));
      Assert.That(result.Host, Is.EqualTo("imap.example.com"));
      Assert.That(result.Port, Is.EqualTo(993));
      Assert.That(result.Security, Is.EqualTo(SecureSocketOptions.SslOnConnect));
    });
  }

  [Test]
  public void CreateImapSettings_Throws_WhenSecurityIsInvalid()
  {
    // Given.
    var mailServerSection = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailServer:Imap:Host"] = "imap.example.com",
      ["MailServer:Imap:Port"] = "993",
      ["MailServer:Imap:Security"] = "bad-value",
    }).GetSection("MailServer");

    // When.
    var act = () => Settings.CreateImapSettings(mailServerSection, _fixture.Create<string>(), _fixture.Create<string>());

    // Then.
    Assert.That(act, Throws.TypeOf<InvalidOperationException>()
      .With.Message.Contains("Invalid security setting"));
  }

  [Test]
  public void CreateEwsSettings_ReturnsConfiguredValues()
  {
    // Given.
    var username = _fixture.Create<string>();
    var password = _fixture.Create<string>();
    var mailServerSection = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailServer:Ews:Url"] = "https://mail.example.com/EWS/Exchange.asmx",
      ["MailServer:Ews:Domain"] = "EXAMPLE",
    }).GetSection("MailServer");

    // When.
    var result = Settings.CreateEwsSettings(mailServerSection, username, password);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Username, Is.EqualTo(username));
      Assert.That(result.Password, Is.EqualTo(password));
      Assert.That(result.Url, Is.EqualTo("https://mail.example.com/EWS/Exchange.asmx"));
      Assert.That(result.Domain, Is.EqualTo("EXAMPLE"));
    });
  }

  [Test]
  public void GetMailImportBackgroundSettings_ReturnsConfiguredValues()
  {
    // Given.
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailImport:Enabled"] = "true",
      ["MailImport:RunOnStartup"] = "false",
      ["MailImport:Interval"] = "01:00:00",
      ["MailImport:InitialLookbackPeriod"] = "1.00:00:00",
      ["MailImport:OverlapPeriod"] = "00:30:00",
      ["MailImport:Folders:0"] = "/",
      ["MailImport:Folders:1"] = "Releases",
    });

    // When.
    var result = Settings.GetMailImportBackgroundSettings(configuration);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Enabled, Is.True);
      Assert.That(result.RunOnStartup, Is.False);
      Assert.That(result.Interval, Is.EqualTo(TimeSpan.FromHours(1)));
      Assert.That(result.InitialLookbackPeriod, Is.EqualTo(TimeSpan.FromDays(1)));
      Assert.That(result.OverlapPeriod, Is.EqualTo(TimeSpan.FromMinutes(30)));
      Assert.That(result.Folders, Is.EqualTo(new[] { "/", "Releases" }));
    });
  }

  [Test]
  public void GetMailImportBackgroundSettings_ReturnsDefaults_WhenSectionIsMissing()
  {
    // Given.
    var configuration = BuildConfiguration(new Dictionary<string, string?>());

    // When.
    var result = Settings.GetMailImportBackgroundSettings(configuration);

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Enabled, Is.False);
      Assert.That(result.RunOnStartup, Is.True);
      Assert.That(result.Interval, Is.EqualTo(TimeSpan.FromHours(1)));
      Assert.That(result.InitialLookbackPeriod, Is.EqualTo(TimeSpan.FromDays(1)));
      Assert.That(result.OverlapPeriod, Is.EqualTo(TimeSpan.FromMinutes(30)));
      Assert.That(result.Folders, Is.EqualTo(new[] { "/" }));
    });
  }

  [Test]
  public void GetMailImportBackgroundSettings_Throws_WhenIntervalIsInvalid()
  {
    // Given.
    var configuration = BuildConfiguration(new Dictionary<string, string?>
    {
      ["MailImport:Interval"] = "bad-value",
    });

    // When.
    var act = () => Settings.GetMailImportBackgroundSettings(configuration);

    // Then.
    Assert.That(act, Throws.TypeOf<InvalidOperationException>()
      .With.Message.Contains("MailImport:Interval"));
  }

  private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }
}
