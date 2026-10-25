namespace Clinic.Appointment.Service.Persistence.Configurations;

using Clinic.Appointment.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PatientProjectionConfiguration : IEntityTypeConfiguration<PatientProjection>
{
    public void Configure(EntityTypeBuilder<PatientProjection> builder)
    {
        builder.ToTable("PatientProjections");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.PhoneNumber).HasMaxLength(20);
        builder.Property(p => p.Gender).HasMaxLength(10);

        builder.HasIndex(p => p.IdentityUserId);
        builder.HasIndex(p => p.PersonId);
    }
}

