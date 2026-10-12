using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class PatientConfiguration : IEntityTypeConfiguration<Patient>
    {
        public void Configure(EntityTypeBuilder<Patient> builder)
        {
            builder.ToTable("Patients");

            builder.HasKey(p => p.Id);

            // Quan hệ 0..1 - 0..1 với Person: cho phép khách vãng lai
            // chưa từng có User vẫn có Patient hợp lệ.
            builder.HasOne(p => p.Person)
                .WithOne(pe => pe.Patient)
                .HasForeignKey<Patient>(p => p.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => p.PersonId).IsUnique();

            builder.Property(p => p.InsuranceNumber).HasMaxLength(50);
            builder.Property(p => p.EmergencyContact).HasMaxLength(200);

            builder.HasData(SampleData.Patients);
        }
    }
}
