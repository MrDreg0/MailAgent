using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Mail;
using MailAgent.Database.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace MailAgent.Database.Tests;

[TestFixture]
public class PostgreSqlDependencyInjectionTests
{
  [Test]
  public void AddPostgreSqlDataContext_RegistersExpectedServices()
  {
    // Given.
    var services = new ServiceCollection();

    // When.
    services.AddPostgreSqlDataContext("Host=example;Database=testdb;Username=test;Password=test");
    using var serviceProvider = services.BuildServiceProvider();
    using var scope = serviceProvider.CreateScope();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(scope.ServiceProvider.GetService<PostgreSqlDataContext>(), Is.Not.Null);
      Assert.That(scope.ServiceProvider.GetService<DataContext>(), Is.Not.Null);
      Assert.That(scope.ServiceProvider.GetService<IMailRepository>(), Is.TypeOf<MailRepository>());
      Assert.That(scope.ServiceProvider.GetService<IDailyDigestRepository>(), Is.TypeOf<DailyDigestRepository>());
    });
  }

  [Test]
  public void AddPostgreSqlDataContext_UsesConnectionStringFactoryOverload()
  {
    // Given.
    var services = new ServiceCollection();

    // When.
    services.AddPostgreSqlDataContext(_ => "Host=example;Database=testdb;Username=test;Password=test");
    using var serviceProvider = services.BuildServiceProvider();
    using var scope = serviceProvider.CreateScope();

    // Then.
    Assert.Multiple(() =>
    {
      Assert.That(scope.ServiceProvider.GetService<PostgreSqlDataContext>(), Is.Not.Null);
      Assert.That(scope.ServiceProvider.GetService<DataContext>(), Is.Not.Null);
      Assert.That(scope.ServiceProvider.GetService<IMailRepository>(), Is.TypeOf<MailRepository>());
      Assert.That(scope.ServiceProvider.GetService<IDailyDigestRepository>(), Is.TypeOf<DailyDigestRepository>());
    });
  }
}
