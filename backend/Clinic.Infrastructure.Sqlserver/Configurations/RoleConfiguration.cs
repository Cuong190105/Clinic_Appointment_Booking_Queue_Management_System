using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(r => r.Name).IsUnique();

            builder.HasData(
                new Role(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Admin", "Administrator role", new DateTime(2026, 1, 1, 0, 0, 0), null, false),
                new Role(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Patient", "Patient role", new DateTime(2026, 1, 1, 0, 0, 0), null, false),
                new Role(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Receptionist", "Receptionist role", new DateTime(2026, 1, 1, 0, 0, 0), null, false),
                new Role(Guid.Parse("00000000-0000-0000-0000-000000000004"), "Doctor", "Doctor role", new DateTime(2026, 1, 1, 0, 0, 0), null, false)
            );

            builder.Property(r => r.Description).HasMaxLength(300);
        }
    }
}
