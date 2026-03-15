using Microsoft.EntityFrameworkCore;
namespace MailAgent.Database;

public class DataContext(DbContextOptions options) : DbContext(options)
{
  public DbSet<MailRecord> Mails { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<MailRecord>()
      .HasIndex(mail => mail.MessageId)
      .IsUnique();
  }
}
