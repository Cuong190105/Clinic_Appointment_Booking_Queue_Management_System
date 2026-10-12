using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
    {
        public void Configure(EntityTypeBuilder<Doctor> builder)
        {
            builder.ToTable("Doctors");

            builder.HasKey(d => d.Id);

            builder.HasOne(d => d.Employee)
                .WithOne(e => e.Doctor)
                .HasForeignKey<Doctor>(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(d => d.EmployeeId).IsUnique();

            builder.Property(d => d.LicenseNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(d => d.Qualification).HasMaxLength(300);
            builder.Property(d => d.Biography).HasMaxLength(1000);

            builder.Property(d => d.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasData(SampleData.Doctors);
        }
    }
}
