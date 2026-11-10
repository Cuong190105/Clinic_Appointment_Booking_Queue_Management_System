using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");

            builder.HasKey(rt => rt.Id);

            builder.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade); // XÃ³a User -> xÃ³a luÃ´n token cá»§a há», khÃ´ng cÃ³ báº£ng nÃ o phá»¥ thuá»™c RefreshToken.

            builder.Property(rt => rt.TokenHash)
                .IsRequired()
                .HasMaxLength(500);

            // Tra cá»©u nhanh khi refresh token gá»­i lÃªn Ä‘á»ƒ xÃ¡c thá»±c.
            builder.HasIndex(rt => rt.TokenHash).IsUnique();
        }
    }
}
