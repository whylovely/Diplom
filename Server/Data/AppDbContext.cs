using Microsoft.EntityFrameworkCore;
using Server.Entities;

namespace Server.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<UserEntity>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
        });

        b.Entity<CategoryEntity>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.Name })
             .IsUnique()
             .HasFilter("\"IsDeleted\" = false");
        });

        b.Entity<AccountEntity>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();

            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.Name })
             .IsUnique()
             .HasFilter("\"IsDeleted\" = false");
        });

        b.Entity<TransactionEntity>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasMany(x => x.Entries)
             .WithOne(x => x.Transaction)
             .HasForeignKey(x => x.TransactionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.Date });
        });

        b.Entity<EntryEntity>(e =>
        {
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.UserId, x.AccountId });
            e.HasIndex(x => new { x.UserId, x.CategoryId });
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

    }
}