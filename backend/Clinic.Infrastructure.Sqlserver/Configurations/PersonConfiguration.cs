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

            // Cho phÃ©p bá»‡nh nhÃ¢n khÃ´ng cÃ³ sá»‘ Ä‘iá»‡n thoáº¡i (vÃ£ng lai hoáº·c tráº» nhá», khÃ´ng Ä‘Äƒng kÃ½ tÃ i khoáº£n).
            builder.Property(p => p.PhoneNumber)
                //.IsRequired()
                .HasMaxLength(20);

            // UNIQUE: trÃ¡nh táº¡o trÃ¹ng há»“ sÆ¡ Person khi khÃ¡ch vÃ£ng lai
            // sau nÃ y tá»± Ä‘Äƒng kÃ½ tÃ i khoáº£n (xem PersonId nullable á»Ÿ User/Patient).
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
