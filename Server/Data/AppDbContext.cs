using Microsoft.EntityFrameworkCore;
using Server.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Server.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<EntryEntity> Entries => Set<EntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionEntity>()
            .HasMany(t => t.Entries)
            .WithOne(e => e.Transaction!)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}