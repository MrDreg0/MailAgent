using MailAgent.Api.Configuration;
using MailAgent.Api.BackgroundServices;
using MailAgent.Application.Digest;
using MailAgent.Application.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MailAgent.Api.Tests;

[TestFixture]
public class SettingsTests
{
  [Test]
  public void AddValidatedConfiguration_RegistersRuntimeSettings_WhenConfigurationIsValid()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = CreateValidConfiguration();

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();

    var llmSettings = serviceProvider.GetRequiredService<LlmSettings>();
    var importSettings = serviceProvider.GetRequiredService<MailImportBackgroundSettings>();
    var dailyDigestSettings = serviceProvider.GetRequiredService<DailyDigestBackgroundSettings>();
    var digestSettings = serviceProvider.GetRequiredService<DailyDigestSettings>();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(llmSettings.Provider, Is.EqualTo(LlmProvider.LmStudio));
      Assert.That(llmSettings.BaseUrl, Is.EqualTo("http://localhost:1234/v1/"));
      Assert.That(llmSettings.Timeout, Is.EqualTo(TimeSpan.FromMinutes(12)));
      Assert.That(llmSettings.FastModel, Is.EqualTo("fast-local-model"));
      Assert.That(llmSettings.MainModel, Is.EqualTo("main-local-model"));
      Assert.That(importSettings.Enabled, Is.False);
      Assert.That(importSettings.RunOnStartup, Is.Null);
      Assert.That(importSettings.Interval, Is.Null);
      Assert.That(importSettings.Folders, Is.Empty);
      Assert.That(dailyDigestSettings.Enabled, Is.False);
      Assert.That(dailyDigestSettings.RunOnStartup, Is.Null);
      Assert.That(dailyDigestSettings.Interval, Is.Null);
      Assert.That(dailyDigestSettings.Folder, Is.Null);
      Assert.That(dailyDigestSettings.InitialBackfillPeriod, Is.Null);
      Assert.That(digestSettings.OutputLanguage, Is.EqualTo("Russian"));
    });
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenLlmBaseUrlIsMissing()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["Llm:BaseUrl"] = null;
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<LlmSettings>();

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("BaseUrl configuration is missing."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenDatabaseConnectionStringIsMissing()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["ConnectionStrings:Database"] = null;
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value;

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("Database configuration is missing."));
  }

  [Test]
  public void AddValidatedConfiguration_AllowsDisabledMailImportWithoutSchedule()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = CreateValidConfiguration();

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();

    var result = serviceProvider.GetRequiredService<MailImportBackgroundSettings>();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(result.Enabled, Is.False);
      Assert.That(result.RunOnStartup, Is.Null);
      Assert.That(result.Interval, Is.Null);
      Assert.That(result.InitialLookbackPeriod, Is.Null);
      Assert.That(result.OverlapPeriod, Is.Null);
      Assert.That(result.Folders, Is.Empty);
    });
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenMailImportEnabledWithoutSchedule()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailImport:Enabled"] = "true";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<MailImportBackgroundSettings>();

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("RunOnStartup configuration is missing."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenMailImportEnabledWithoutFolders()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailImport:Enabled"] = "true";
    configurationValues["MailImport:RunOnStartup"] = "true";
    configurationValues["MailImport:Interval"] = "01:00:00";
    configurationValues["MailImport:InitialLookbackPeriod"] = "1.00:00:00";
    configurationValues["MailImport:OverlapPeriod"] = "00:30:00";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<MailImportBackgroundSettings>();

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("Folders must contain at least one folder when Enabled is true."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenDailyDigestEnabledWithoutFolder()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["DailyDigest:Enabled"] = "true";
    configurationValues["DailyDigest:RunOnStartup"] = "true";
    configurationValues["DailyDigest:Interval"] = "01:00:00";
    configurationValues["DailyDigest:GenerateAfter"] = "08:00:00";
    configurationValues["DailyDigest:InitialBackfillPeriod"] = "7.00:00:00";
    configurationValues["DailyDigest:OutputLanguage"] = "Russian";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<DailyDigestBackgroundSettings>();

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("Folder configuration is missing."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenDailyDigestEnabledWithoutOutputLanguage()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["DailyDigest:Enabled"] = "true";
    configurationValues["DailyDigest:RunOnStartup"] = "true";
    configurationValues["DailyDigest:Interval"] = "01:00:00";
    configurationValues["DailyDigest:Folder"] = "Releases";
    configurationValues["DailyDigest:GenerateAfter"] = "08:00:00";
    configurationValues["DailyDigest:InitialBackfillPeriod"] = "7.00:00:00";
    configurationValues["DailyDigest:OutputLanguage"] = null;
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<DailyDigestBackgroundSettings>();

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("OutputLanguage configuration is missing."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenImapSecurityIsInvalid()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailServer:Imap:Security"] = "bad-value";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<IOptions<MailServerConfiguration>>().Value;

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("Invalid Imap.Security setting"));
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
      ["Llm:Provider"] = nameof(LlmProvider.LmStudio),
      ["Llm:BaseUrl"] = "http://localhost:1234/v1/",
      ["Llm:TimeoutMinutes"] = "12",
      ["Llm:FastModel"] = "fast-local-model",
      ["Llm:MainModel"] = "main-local-model",
      ["MailImport:Enabled"] = "false",
      ["MailImport:RunOnStartup"] = null,
      ["MailImport:Interval"] = null,
      ["MailImport:InitialLookbackPeriod"] = null,
      ["MailImport:OverlapPeriod"] = null,
      ["DailyDigest:Enabled"] = "false",
      ["DailyDigest:RunOnStartup"] = null,
      ["DailyDigest:Interval"] = null,
      ["DailyDigest:Folder"] = null,
      ["DailyDigest:OutputLanguage"] = "Russian",
      ["DailyDigest:InitialBackfillPeriod"] = null,
      ["DailyDigest:GenerateAfter"] = null,
    };
  }

  private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }
}
