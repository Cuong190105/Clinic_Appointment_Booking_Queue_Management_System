using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class BookingSlotConfiguration : IEntityTypeConfiguration<BookingSlot>
    {
        public void Configure(EntityTypeBuilder<BookingSlot> builder)
        {
            builder.ToTable("BookingSlots");

            builder.HasKey(slot => slot.Id);

            builder.Property(slot => slot.TimeSlot)
                .HasColumnType("time")
                .IsRequired();

            builder.Property(slot => slot.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.HasOne(slot => slot.WorkSchedule)
                .WithMany()
                .HasForeignKey(slot => slot.WorkScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(slot => new { slot.WorkScheduleId, slot.TimeSlot })
                .IsUnique();
        }
    }
}
