using Microsoft.EntityFrameworkCore;
namespace MailAgent.Database;

public class DataContext(DbContextOptions options) : DbContext(options)
{
  public DbSet<MailRecord> Mails { get; set; }

  public DbSet<DailyDigestRecord> DailyDigests { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<MailRecord>()
      .HasIndex(mail => mail.MessageId)
      .IsUnique();

    modelBuilder.Entity<DailyDigestRecord>()
      .HasIndex(digest => new { digest.Folder, digest.DigestDate })
      .IsUnique();
  }
}
