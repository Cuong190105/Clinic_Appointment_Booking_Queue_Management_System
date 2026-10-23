using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Appointment.Service.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Appointments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_IdempotencyKey",
                table: "Appointments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_IdempotencyKey",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Appointments");
        }
    }
}
