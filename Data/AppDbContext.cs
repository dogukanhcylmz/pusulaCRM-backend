using Microsoft.EntityFrameworkCore;
using DigitestCrm.Api.Models;

namespace DigitestCrm.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();

    /// Tüm tarihler veritabanında UTC saklanr
    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        cb.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        cb.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.CompanyName);
        });

        b.Entity<Activity>(e =>
        {
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            e.Property(x => x.Type).HasConversion<string>();
            e.HasOne(x => x.Customer).WithMany(c => c.Activities)
             .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Quote>(e =>
        {
            e.Property(x => x.QuoteNumber).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.QuoteNumber).IsUnique();
            e.Property(x => x.Status).HasConversion<string>();
            e.Ignore(x => x.SubTotal);
            e.Ignore(x => x.TaxAmount);
            e.Ignore(x => x.GrandTotal);
            e.HasOne(x => x.Customer).WithMany(c => c.Quotes)
             .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Items).WithOne()
             .HasForeignKey(i => i.QuoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<QuoteItem>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Ignore(x => x.LineTotal);
        });
    }
}
