using FairShare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairShare.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.RefreshToken)
            .HasMaxLength(512);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Navigation: groups owned
        builder.HasMany(u => u.OwnedGroups)
            .WithOne(g => g.CreatedByUser)
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Navigation: group memberships
        builder.HasMany(u => u.GroupMemberships)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
