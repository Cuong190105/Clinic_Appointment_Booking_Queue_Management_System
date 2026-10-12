using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class PersonConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.ToTable("Persons");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.FullName)
                .IsRequired()
                .HasMaxLength(200);

            // Cho phép bệnh nhân không có số điện thoại (vãng lai hoặc trẻ nhỏ, không đăng ký tài khoản).
            builder.Property(p => p.PhoneNumber)
                //.IsRequired()
                .HasMaxLength(20);

            // UNIQUE: tránh tạo trùng hồ sơ Person khi khách vãng lai
            // sau này tự đăng ký tài khoản (xem PersonId nullable ở User/Patient).
            builder.HasIndex(p => p.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");

            builder.Property(p => p.Email).HasMaxLength(200);
            builder.Property(p => p.Address).HasMaxLength(500);

            builder.HasMany(p => p.Notifications)
                .WithOne(n => n.Person)
                .HasForeignKey(n => n.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasData(SampleData.Persons);
        }
    }
}
