using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");

            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Person)
                .WithOne(p => p.Employee)
                .HasForeignKey<Employee>(e => e.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.PersonId).IsUnique();

            // Tá»± tham chiáº¿u (quáº£n lÃ½ cáº¥p trÃªn) - optional, khÃ´ng cÃ³ navigation
            // collection ngÆ°á»£c láº¡i (Subordinates) vÃ¬ hiá»‡n chÆ°a dÃ¹ng Ä‘áº¿n.
            builder.HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasData(SampleData.Employees);
        }
    }
}
