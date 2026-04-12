using MailAgent.Database.PostgreSql;
using MailAgent.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MailAgent.Web.Tests;

[TestFixture]
public class SettingsTests
{
  [Test]
  public void AddValidatedConfiguration_RegistersOptions_WhenConfigurationIsValid()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = CreateValidConfiguration();

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var connectionStrings = serviceProvider.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value;
    var mailAgentApi = serviceProvider.GetRequiredService<IOptions<MailAgentApiConfiguration>>().Value;
    var webHost = serviceProvider.GetRequiredService<IOptions<WebHostConfiguration>>().Value;

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(connectionStrings.Database, Is.EqualTo("Host=localhost;Database=mailagent;Username=postgres;Password=postgres"));
      Assert.That(mailAgentApi.BaseUrl, Is.EqualTo("http://localhost:8080"));
      Assert.That(mailAgentApi.TimeoutMinutes, Is.EqualTo(10));
      Assert.That(webHost.UseHttpsRedirection, Is.EqualTo("false"));
    });
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
  public void AddValidatedConfiguration_Throws_WhenUseHttpsRedirectionIsInvalid()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["UseHttpsRedirection"] = "sometimes";
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<IOptions<WebHostConfiguration>>().Value;

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("UseHttpsRedirection must be a valid boolean."));
  }

  [Test]
  public void AddValidatedConfiguration_Throws_WhenMailAgentApiBaseUrlIsMissing()
  {
    // Given.
    var services = new ServiceCollection();
    var configurationValues = CreateValidConfigurationValues();
    configurationValues["MailAgentApi:BaseUrl"] = null;
    var configuration = BuildConfiguration(configurationValues);

    // When.
    services.AddValidatedConfiguration(configuration);
    using var serviceProvider = services.BuildServiceProvider();
    var act = () => serviceProvider.GetRequiredService<IOptions<MailAgentApiConfiguration>>().Value;

    // Then.
    Assert.That(act, Throws.TypeOf<OptionsValidationException>()
      .With.Message.Contains("BaseUrl configuration is missing."));
  }

  [Test]
  public void AddPostgreSqlDataContext_ResolvesDbContext_WhenConnectionStringComesFromValidatedConfiguration()
  {
    // Given.
    var services = new ServiceCollection();
    var configuration = CreateValidConfiguration();

    // When.
    services.AddValidatedConfiguration(configuration);
    services.AddPostgreSqlDataContext(serviceProvider =>
      serviceProvider.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value.Database!);

    using var serviceProvider = services.BuildServiceProvider();
    using var scope = serviceProvider.CreateScope();

    // Then.
    var dataContext = scope.ServiceProvider.GetRequiredService<PostgreSqlDataContext>();
    Assert.That(dataContext, Is.Not.Null);
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
      ["MailAgentApi:BaseUrl"] = "http://localhost:8080",
      ["MailAgentApi:TimeoutMinutes"] = "10",
      ["UseHttpsRedirection"] = "false",
    };
  }

  private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> values)
  {
    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }
}
