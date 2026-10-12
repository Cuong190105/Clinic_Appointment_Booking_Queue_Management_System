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

            // AppointmentId bắt buộc + UNIQUE (Cách B):
            // 1 Appointment sinh tối đa 1 QueueTicket, mọi QueueTicket đều
            // phải xuất phát từ 1 Appointment (kể cả walk-in, xem Appointment.IsWalkIn).
            builder.HasOne(q => q.Appointment)
                .WithOne(a => a.QueueTicket)
                .HasForeignKey<QueueTicket>(q => q.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(q => q.AppointmentId).IsUnique();

            builder.Property(q => q.QueueNumber).IsRequired();

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
