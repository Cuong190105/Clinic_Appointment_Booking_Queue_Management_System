using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class QueueTicketConfiguration : IEntityTypeConfiguration<QueueTicket>
    {
        public void Configure(EntityTypeBuilder<QueueTicket> builder)
        {
            builder.ToTable("QueueTickets");

            builder.HasKey(q => q.Id);

            // AppointmentId báº¯t buá»™c + UNIQUE (CÃ¡ch B):
            // 1 Appointment sinh tá»‘i Ä‘a 1 QueueTicket, má»i QueueTicket Ä‘á»u
            // pháº£i xuáº¥t phÃ¡t tá»« 1 Appointment (ká»ƒ cáº£ walk-in, xem Appointment.IsWalkIn).
            builder.HasOne(q => q.Appointment)
                .WithOne(a => a.QueueTicket)
                .HasForeignKey<QueueTicket>(q => q.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(q => q.AppointmentId).IsUnique();

            builder.Property(q => q.QueueNumber).IsRequired();

            builder.Property(q => q.PatientName).HasMaxLength(200);
            builder.Property(q => q.DoctorName).HasMaxLength(200);

            builder.Property(q => q.CheckInTime).IsRequired();

            builder.Property(q => q.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(q => q.RowVersion)
                .IsRowVersion();

            builder.HasData(SampleData.QueueTickets);
        }
    }
}
