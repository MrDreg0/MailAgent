using Microsoft.EntityFrameworkCore;
namespace MailAgent.Database.PostgreSql;

public class PostgreSqlDataContext : DataContext
{
  public PostgreSqlDataContext(DbContextOptions<PostgreSqlDataContext> options) : base(options)
  {
  }
}
