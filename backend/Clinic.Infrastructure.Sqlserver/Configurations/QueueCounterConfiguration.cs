using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class QueueCounterConfiguration : IEntityTypeConfiguration<QueueCounter>
    {
        public void Configure(EntityTypeBuilder<QueueCounter> builder)
        {
            builder.ToTable("QueueCounters");

            // Khóa chính composite (DoctorId, Date): tự đảm bảo duy nhất
            // 1 dòng counter / bác sĩ / ngày, không cần thêm unique index riêng.
            builder.HasKey(c => new { c.DoctorId, c.Date });

            builder.Property(c => c.Date)
                .HasColumnType("date");

            builder.Property(c => c.CurrentNumber)
                .IsRequired();
        }
    }
}