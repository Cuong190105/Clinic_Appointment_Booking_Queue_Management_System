using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(u => u.Username).IsUnique();

            builder.Property(u => u.PasswordHash).IsRequired();

            // Quan hệ 0..1 - 0..1 với Person: 1 Person chỉ có tối đa 1 User.
            builder.HasOne(u => u.Person)
                .WithOne(p => p.User)
                .HasForeignKey<User>(u => u.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(u => u.PersonId).IsUnique();

            builder.HasData(SampleData.Users);
        }
    }
}
