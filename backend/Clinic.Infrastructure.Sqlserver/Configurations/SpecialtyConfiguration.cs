using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
    {
        public void Configure(EntityTypeBuilder<Specialty> builder)
        {
            builder.ToTable("Specialties");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(s => s.EstablishedDate).IsRequired();

            builder.HasIndex(s => s.Name).IsUnique().HasFilter("[IsDeleted] = 0");

            builder.Property(s => s.Description).HasMaxLength(500);

            builder.HasData(SampleData.Specialties);
        }
    }
}
