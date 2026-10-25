using Clinic.Queue.Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Queue.Service.Persistence.Configurations;

public sealed class CancelledAppointmentConfiguration : IEntityTypeConfiguration<CancelledAppointment>
{
    public void Configure(EntityTypeBuilder<CancelledAppointment> builder)
    {
        builder.ToTable("CancelledAppointments");
        builder.HasKey(x => x.AppointmentId);
    }
}
