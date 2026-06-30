using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.TotalAmount)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        // Note: query filter for IsDeleted is set on the DbContext level
        builder.HasMany(e => e.Payers)
            .WithOne(p => p.Expense)
            .HasForeignKey(p => p.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Splits)
            .WithOne(s => s.Expense)
            .HasForeignKey(s => s.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
