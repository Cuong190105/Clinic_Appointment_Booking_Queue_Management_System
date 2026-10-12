using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class ShiftRequestConfiguration : IEntityTypeConfiguration<ShiftRequest>
    {
        public void Configure(EntityTypeBuilder<ShiftRequest> builder)
        {
            builder.ToTable("ShiftRequests");

            builder.HasKey(sr => sr.Id);

            builder.HasOne(sr => sr.Doctor)
                .WithMany(d => d.ShiftRequests)
                .HasForeignKey(sr => sr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(sr => sr.Reason).HasMaxLength(500);

            builder.Property(sr => sr.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        }
    }
}
