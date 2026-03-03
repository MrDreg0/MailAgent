using Microsoft.EntityFrameworkCore;
namespace MailAgent.Database;

public class DataContext : DbContext
{
  public DbSet<MailDto> Mails { get; set; }
  
  public DataContext(DbContextOptions options) : base(options)
  {
  }
}
