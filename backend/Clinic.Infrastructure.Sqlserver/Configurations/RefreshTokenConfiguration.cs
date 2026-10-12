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
                .OnDelete(DeleteBehavior.Cascade); // Xóa User -> xóa luôn token của họ, không có bảng nào phụ thuộc RefreshToken.

            builder.Property(rt => rt.TokenHash)
                .IsRequired()
                .HasMaxLength(500);

            // Tra cứu nhanh khi refresh token gửi lên để xác thực.
            builder.HasIndex(rt => rt.TokenHash).IsUnique();
        }
    }
}
