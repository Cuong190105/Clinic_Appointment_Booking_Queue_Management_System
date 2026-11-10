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

            // Khóa chính composite (DoctorId, Date): t? d?m b?o duy nh?t
            // 1 dòng counter / bác si / ngày, không c?n thêm unique index riêng.
            builder.HasKey(c => new { c.DoctorId, c.Date });

            builder.Property(c => c.Date)
                .HasColumnType("date");

            builder.Property(c => c.CurrentNumber)
                .IsRequired();
        }
    }
}