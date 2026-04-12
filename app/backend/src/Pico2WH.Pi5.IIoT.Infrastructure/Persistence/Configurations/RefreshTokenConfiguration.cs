using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pico2WH.Pi5.IIoT.Domain.Entities;

namespace Pico2WH.Pi5.IIoT.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("token_id");

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(r => r.TokenHash).IsRequired();
        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.Property(r => r.ExpiresAtUtc).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.IssuedAtUtc).HasColumnName("issued_at").IsRequired();
        builder.Property(r => r.IsRevoked).HasColumnName("is_revoked").IsRequired();
        builder.Property(r => r.RevokedAtUtc).HasColumnName("revoked_at");
        builder.Property(r => r.RevokedReason).HasMaxLength(64).HasColumnName("revoked_reason");

        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
