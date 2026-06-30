using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.ToTable("expense_splits");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShareAmount)
            .IsRequired()
            .HasColumnType("numeric(18,2)");
    }
}
