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

            // Tự tham chiếu (quản lý cấp trên) - optional, không có navigation
            // collection ngược lại (Subordinates) vì hiện chưa dùng đến.
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
