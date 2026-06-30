using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.GuestName).HasMaxLength(80);

        builder.Property(m => m.BudgetCap)
            .HasColumnType("numeric(18,2)");

        builder.Property(m => m.JoinedAt).IsRequired();

        builder.HasMany(m => m.PayerRecords)
            .WithOne(p => p.Member)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.SplitRecords)
            .WithOne(s => s.Member)
            .HasForeignKey(s => s.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.CreatedExpenses)
            .WithOne(e => e.CreatedByMember)
            .HasForeignKey(e => e.CreatedByMemberId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
