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
    services.AddDbContext<PostgreSqlDataContext>(options =>
    {
      options.UseNpgsql(connectionString);

      if (enableSensitiveDataLogging)
      {
        options.EnableSensitiveDataLogging();
      }
    });
    services.AddScoped<DataContext>(serviceProvider => serviceProvider.GetRequiredService<PostgreSqlDataContext>());
    services.AddScoped<IMailRepository, MailRepository>();

    return services;
  }
}
