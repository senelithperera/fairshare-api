using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Infrastructure.Persistence;

/// <summary>EF Core database context for FairShare.</summary>
public class FairShareDbContext : DbContext
{
    public FairShareDbContext(DbContextOptions<FairShareDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpensePayer> ExpensePayers => Set<ExpensePayer>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FairShareDbContext).Assembly);

        // Global query filters
        modelBuilder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Group>().HasQueryFilter(g => !g.IsArchived);
    }
}
