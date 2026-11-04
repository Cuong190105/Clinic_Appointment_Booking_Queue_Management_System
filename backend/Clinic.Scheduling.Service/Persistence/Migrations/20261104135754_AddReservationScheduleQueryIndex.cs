using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Scheduling.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationScheduleQueryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SlotReservations_WorkScheduleId_Status_ExpiresAt",
                table: "SlotReservations",
                columns: new[] { "WorkScheduleId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SlotReservations_WorkScheduleId_Status_ExpiresAt",
                table: "SlotReservations");
        }
    }
}
