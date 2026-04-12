using MailAgent.Api.BackgroundServices;
using MailAgent.Api.Configuration;
using MailAgent.Application;
using MailAgent.Application.Digest;
using MailAgent.Application.Contracts.Llm;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Application.Exceptions;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MailAgent.Api.Tests;

[TestFixture]
public class DependencyInjectionTests
{
  [Test]
  public void AddConfiguredMailClient_RegistersImapMailClient_WhenProviderIsImap()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = CreateValidConfiguration();

    // When.
    services.AddValidatedConfiguration(configuration);
    services.AddConfiguredMailClient();
    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    var mailClient = serviceProvider.GetService<IMailClient>();
    Assert.That(mailClient, Is.Not.Null);
    Assert.That(mailClient!.GetType().FullName, Is.EqualTo("MailAgent.Mail.Imap.MailClient"));
  }

  [Test]
  public void AddConfiguredMailClient_RegistersEwsMailClient_WhenProviderIsEws()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailServer:Provider"] = nameof(MailProvider.Ews);
    configurationValues["MailServer:Ews:Url"] = "https://ews.example.com/EWS/Exchange.asmx";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    services.AddConfiguredMailClient();
    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    var mailClient = serviceProvider.GetService<IMailClient>();
    Assert.That(mailClient, Is.Not.Null);
    Assert.That(mailClient!.GetType().FullName, Is.EqualTo("MailAgent.Mail.Ews.MailClient"));
  }

  [Test]
  public void AddConfiguredMailClient_Throws_WhenProviderIsUnsupported()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddSingleton<IOptions<MailServerConfiguration>>(Options.Create(new MailServerConfiguration
    {
      Provider = (MailProvider)999,
      Username = "user@example.com",
      Password = "secret",
      Imap = new MailServerImapConfiguration
      {
        Host = "imap.example.com",
        Port = "993",
        Security = "SslOnConnect",
      },
      Ews = new MailServerEwsConfiguration
      {
        Url = "https://ews.example.com/EWS/Exchange.asmx",
      }
    }));

    // When.
    services.AddConfiguredMailClient();
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<IMailClient>();

    // Then.
    Assert.That(act, Throws.TypeOf<UnsupportedMailProviderException>()
      .With.Message.Contains("Unsupported mail provider"));
  }

  [Test]
  public void AddMailImportBackgroundService_RegistersHostedServiceAndSettings()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddLogging();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailImport:Enabled"] = "true";
    configurationValues["MailImport:RunOnStartup"] = "true";
    configurationValues["MailImport:Interval"] = "01:00:00";
    configurationValues["MailImport:InitialLookbackPeriod"] = "1.00:00:00";
    configurationValues["MailImport:OverlapPeriod"] = "00:30:00";
    configurationValues["MailImport:Folders:0"] = "/";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    services.AddMailImportBackgroundService();
    using var serviceProvider = services.BuildServiceProvider();
    var settings = serviceProvider.GetRequiredService<MailImportBackgroundSettings>();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(settings.Enabled, Is.True);
      Assert.That(settings.RunOnStartup, Is.True);
      Assert.That(settings.Interval, Is.EqualTo(TimeSpan.FromHours(1)));
      Assert.That(settings.InitialLookbackPeriod, Is.EqualTo(TimeSpan.FromDays(1)));
      Assert.That(settings.OverlapPeriod, Is.EqualTo(TimeSpan.FromMinutes(30)));
      Assert.That(settings.Folders, Is.EqualTo(new[] { "/" }));
    });
    Assert.That(
      serviceProvider.GetServices<IHostedService>().Any(service => service is MailImportBackgroundService),
      Is.True);
  }

  [Test]
  public void AddDailyDigestBackgroundService_RegistersHostedServiceAndSettings()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddLogging();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["DailyDigest:Enabled"] = "true";
    configurationValues["DailyDigest:RunOnStartup"] = "true";
    configurationValues["DailyDigest:Interval"] = "01:00:00";
    configurationValues["DailyDigest:Folder"] = "Releases";
    configurationValues["DailyDigest:OutputLanguage"] = "Russian";
    configurationValues["DailyDigest:InitialBackfillPeriod"] = "7.00:00:00";
    configurationValues["DailyDigest:GenerateAfter"] = "08:00:00";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    services.AddDailyDigestBackgroundService();
    using var serviceProvider = services.BuildServiceProvider();
    var settings = serviceProvider.GetRequiredService<DailyDigestBackgroundSettings>();
    var digestSettings = serviceProvider.GetRequiredService<DailyDigestSettings>();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(settings.Enabled, Is.True);
      Assert.That(settings.RunOnStartup, Is.True);
      Assert.That(settings.Interval, Is.EqualTo(TimeSpan.FromHours(1)));
      Assert.That(settings.Folder, Is.EqualTo("Releases"));
      Assert.That(settings.InitialBackfillPeriod, Is.EqualTo(TimeSpan.FromDays(7)));
      Assert.That(settings.GenerateAfter, Is.EqualTo(new TimeOnly(8, 0, 0)));
      Assert.That(digestSettings.OutputLanguage, Is.EqualTo("Russian"));
    });
    Assert.That(
      serviceProvider.GetServices<IHostedService>().Any(service => service is DailyDigestBackgroundService),
      Is.True);
  }

  [Test]
  public void AddApplication_RegistersLlmClient_WhenProviderIsOllama()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(CreateLlmSettings(provider: LlmProvider.Ollama));

    // When.
    services.AddApplication();

    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    Assert.That(serviceProvider.GetService<ILlmClient>(), Is.Not.Null);
  }

  [Test]
  public void AddApplication_RegistersLlmClient_WhenProviderIsLmStudio()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(CreateLlmSettings(provider: LlmProvider.LmStudio));

    // When.
    services.AddApplication();

    using var serviceProvider = services.BuildServiceProvider();

    // Then.
    Assert.That(serviceProvider.GetService<ILlmClient>(), Is.Not.Null);
  }

  [Test]
  public void AddApplication_Throws_WhenLlmProviderIsUnsupported()
  {
    // Given.
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(new LlmSettings
    {
      Provider = (LlmProvider)999,
      BaseUrl = "http://localhost:11434/",
      Timeout = TimeSpan.FromMinutes(5),
      FastModel = "llama3.2:3b",
      MainModel = "qwen2.5:7b-instruct",
    });

    // When.
    services.AddApplication();
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<ILlmClient>();

    // Then.
    Assert.That(act, Throws.TypeOf<UnsupportedLlmProviderException>()
      .With.Message.Contains("Unsupported LLM provider"));
  }

  private static IConfiguration CreateValidConfiguration()
  {
    return BuildConfiguration(CreateValidConfigurationValues());
  }

  private static Dictionary<string, string?> CreateValidConfigurationValues()
  {
    return new Dictionary<string, string?>
    {
      ["ConnectionStrings:Database"] = "Host=localhost;Database=mailagent;Username=postgres;Password=postgres",
      ["MailServer:Provider"] = nameof(MailProvider.Imap),
      ["MailServer:Username"] = "user@example.com",
      ["MailServer:Password"] = "secret",
      ["MailServer:Imap:Host"] = "imap.example.com",
      ["MailServer:Imap:Port"] = "993",
      ["MailServer:Imap:Security"] = "SslOnConnect",
      ["Llm:Provider"] = nameof(LlmProvider.Ollama),
      ["Llm:BaseUrl"] = "http://localhost:11434/",
      ["Llm:TimeoutMinutes"] = "5",
      ["Llm:FastModel"] = "llama3.2:3b",
      ["Llm:MainModel"] = "qwen2.5:7b-instruct",
      ["MailImport:Enabled"] = "false",
      ["DailyDigest:Enabled"] = "false",
      ["DailyDigest:OutputLanguage"] = "Russian",
    };
  }

  private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }

  private static LlmSettings CreateLlmSettings(LlmProvider provider)
  {
    return new LlmSettings
    {
      Provider = provider,
      BaseUrl = "http://localhost:11434/",
      Timeout = TimeSpan.FromMinutes(5),
      FastModel = "llama3.2:3b",
      MainModel = "qwen2.5:7b-instruct",
    };
  }
}
