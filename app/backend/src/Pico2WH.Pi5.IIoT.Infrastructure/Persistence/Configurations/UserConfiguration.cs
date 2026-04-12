using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("user_id");

        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.PasswordHash).IsRequired();

        builder.Property(u => u.Role)
            .HasMaxLength(16)
            .HasConversion(
                r => r == UserRole.Admin ? "admin" : "customer",
                v => v == "admin" ? UserRole.Admin : UserRole.Customer);

        builder.Property(u => u.TenantScope).HasMaxLength(64).IsRequired();

        builder.Property(u => u.IsActive).IsRequired();

        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.UpdatedAtUtc);
    }
}
