using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class WorkHistoryConfiguration : IEntityTypeConfiguration<WorkHistory>
    {
        public void Configure(EntityTypeBuilder<WorkHistory> builder)
        {
            builder.ToTable("WorkHistories");

            builder.HasKey(w => w.Id);

            builder.HasOne(w => w.Doctor)
                .WithMany(d => d.WorkHistories)
                .HasForeignKey(w => w.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(w => w.Specialty)
                .WithMany(s => s.WorkHistories)
                .HasForeignKey(w => w.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasData(SampleData.WorkHistories);
        }
    }
}
