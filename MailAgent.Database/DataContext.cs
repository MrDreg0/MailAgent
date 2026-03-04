using Microsoft.EntityFrameworkCore;
namespace MailAgent.Database;

public class DataContext(DbContextOptions options) : DbContext(options)
{
  public DbSet<MailDto> Mails { get; set; }
}
