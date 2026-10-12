using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("RolePermissions");

            // Khóa chính kép (RoleId, PermissionId)
            builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            builder.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed role-permission mappings
            builder.HasData(
                // Admin -> all permissions
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000F") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000014") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000007") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000008") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000C") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000D") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000C") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000F") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000006") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000007") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000008") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000003") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000002") }, // queue.start-exam.any
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000004") }, // queue.complete-exam.any

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000016") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000017") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000015") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000018") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000019") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000020") },


                // Receptionist -> appointment and queue permissions
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000F") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000C") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("1000000A-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000006") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000006") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000007") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000008") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000005") },

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000002") }, // queue.start-exam.any
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000004") }, // queue.complete-exam.any

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000015") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000018") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000007") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000003"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000011") },



                // Doctor -> doctor profile and view appointments
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000013") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000009") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000C") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000B") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000E") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("20000000-0000-0000-0000-000000000006") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000004") },

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000005") }, // queue.start-exam.own
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000003") }, // queue.complete-exam.own

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000015") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000018") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("2000000B-0000-0000-0000-000000000001") }, // shift.suggestion.self-manage

                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000004"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000011") },

                // Patient -> create/view/cancel own appointment
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000001") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000002") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("10000000-0000-0000-0000-00000000000E") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("1000000A-0000-0000-0000-000000000002") }, // appointment.reschedule
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("10000000-0000-0000-0000-000000000005") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000A") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("20000000-0000-0000-0000-00000000000D") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000004") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000008") },
                new { RoleId = Guid.Parse("00000000-0000-0000-0000-000000000002"), PermissionId = Guid.Parse("2000000A-0000-0000-0000-000000000010") }
            );
        }
    }
}