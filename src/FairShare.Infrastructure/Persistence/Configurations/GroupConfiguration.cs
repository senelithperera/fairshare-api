using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.GuestToken)
            .IsRequired();

        builder.HasIndex(g => g.GuestToken).IsUnique();

        builder.Property(g => g.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("LKR");

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.IsArchived).HasDefaultValue(false);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Expenses)
            .WithOne(e => e.Group)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
