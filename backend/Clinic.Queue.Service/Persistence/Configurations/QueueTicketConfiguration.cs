namespace Clinic.Queue.Service.Persistence.Configurations;

using Clinic.Queue.Service.Entities;
using Clinic.Queue.Service.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
{
    public void Configure(EntityTypeBuilder<QueueTicket> builder)
    {
        builder.ToTable("QueueTickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.PatientName)
            .HasMaxLength(200);

        builder.Property(t => t.DoctorName)
            .HasMaxLength(200);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.QueueDate)
            .HasColumnType("date")
            .IsRequired();

        // 1 ticket per appointment
        builder.HasIndex(t => t.AppointmentId)
            .IsUnique()
            .HasDatabaseName("IX_QueueTickets_AppointmentId");

        // Unique queue number per doctor per day
        builder.HasIndex(t => new { t.DoctorId, t.QueueDate, t.QueueNumber })
            .IsUnique()
            .HasDatabaseName("IX_QueueTickets_DoctorId_QueueDate_QueueNumber");

        // Fast queue query index
        builder.HasIndex(t => new { t.DoctorId, t.QueueDate, t.Status })
            .HasDatabaseName("IX_QueueTickets_DoctorId_QueueDate_Status");
    }
}
