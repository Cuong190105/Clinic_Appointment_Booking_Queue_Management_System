using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Appointment.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowWalkInAppointmentsAtSameSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_ScheduleDate_TimeSlot",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_ScheduleDate_TimeSlot",
                table: "Appointments",
                columns: new[] { "DoctorId", "ScheduleDate", "TimeSlot" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [IsDeleted] = 0 AND [IsWalkIn] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_ScheduleDate_TimeSlot",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_ScheduleDate_TimeSlot",
                table: "Appointments",
                columns: new[] { "DoctorId", "ScheduleDate", "TimeSlot" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [IsDeleted] = 0");
        }
    }
}
