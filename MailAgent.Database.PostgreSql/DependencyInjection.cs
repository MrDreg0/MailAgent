using MailAgent.Application.Contracts.Digest;
using MailAgent.Application.Contracts.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace MailAgent.Database.PostgreSql;

public static class DependencyInjection
{
  public static IServiceCollection AddPostgreSqlDataContext(
    this IServiceCollection services,
    string connectionString,
    bool enableSensitiveDataLogging = false)
  {
    return services.AddPostgreSqlDataContext(_ => connectionString, enableSensitiveDataLogging);
  }

  public static IServiceCollection AddPostgreSqlDataContext(
    this IServiceCollection services,
    Func<IServiceProvider, string> connectionStringFactory,
    bool enableSensitiveDataLogging = false)
  {
    ArgumentNullException.ThrowIfNull(connectionStringFactory);

    services.AddDbContext<PostgreSqlDataContext>((serviceProvider, options) =>
    {
      var connectionString = connectionStringFactory(serviceProvider);
      options.UseNpgsql(connectionString);

      if (enableSensitiveDataLogging)
      {
        options.EnableSensitiveDataLogging();
      }
    });
    services.AddScoped<DataContext>(serviceProvider => serviceProvider.GetRequiredService<PostgreSqlDataContext>());
    services.AddScoped<IMailRepository, MailRepository>();
    services.AddScoped<IDailyDigestRepository, DailyDigestRepository>();

    return services;
  }
}
