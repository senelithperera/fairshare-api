using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class ExpensePayerConfiguration : IEntityTypeConfiguration<ExpensePayer>
{
    public void Configure(EntityTypeBuilder<ExpensePayer> builder)
    {
        builder.ToTable("expense_payers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AmountPaid)
            .IsRequired()
            .HasColumnType("numeric(18,2)");
    }
}
