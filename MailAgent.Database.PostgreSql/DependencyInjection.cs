using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace MailAgent.Database.PostgreSql;

public static class DependencyInjection
{
  public static IServiceCollection AddPostgreSqlDataContext(
    this IServiceCollection services,
    string connectionString)
  {
    services.AddDbContext<PostgreSqlDataContext>(options =>
      options
        .EnableSensitiveDataLogging()
        .UseNpgsql(connectionString));
    services.AddScoped<DataContext>(serviceProvider => serviceProvider.GetRequiredService<PostgreSqlDataContext>());
    services.AddScoped<MailAgent.Application.IMailRepository, MailRepository>();

    return services;
  }
}
